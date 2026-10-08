using Hazel.Udp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Hazel.UnitTests
{
    [TestClass]
    public class BroadcastTests
    {
        [TestMethod]
        public void CanStart()
        {
            const string TestData = "pwerowerower";
            int port;

            using (UdpBroadcastListener listener = CreateListener(out port))
            using (UdpBroadcaster caster = new UdpBroadcaster(port))
            {
                listener.StartListen();

                caster.SetData(TestData);

                caster.Broadcast();
                Thread.Sleep(1000);

                BroadcastPacket[] pkt = listener.GetPackets();
                foreach (var p in pkt)
                {
                    Console.WriteLine($"{p.Data} {p.Sender}");
                    Assert.AreEqual(TestData, p.Data);
                }

                Assert.IsTrue(pkt.Length >= 1);
            }
        }

        [TestMethod]
        public void RepeatedPacketFromSameSenderIsCollapsedAndRefreshesReceiveTime()
        {
            int port;
            using (UdpBroadcastListener listener = CreateListener(out port))
            using (Socket sender = CreateSender())
            {
                listener.StartListen();
                Send(sender, port, "same-data");

                List<BroadcastPacket> buffered = WaitForBufferedPackets(listener, "same-data");
                BroadcastPacket original = buffered.Single(packet => packet.Data == "same-data");
                original.ReceiveTime = DateTime.MinValue;

                Send(sender, port, "same-data");
                string barrier = "__hazel_broadcast_test_barrier_repeat";
                Send(sender, port, barrier);

                BroadcastPacket[] packets = ReceiveAfterBarriers(listener, barrier);

                Assert.AreEqual(2, packets.Length);
                BroadcastPacket repeated = packets.Single(packet => packet.Data == "same-data");
                Assert.AreSame(original, repeated);
                Assert.IsTrue(repeated.ReceiveTime > DateTime.MinValue);
                Assert.AreEqual(1, packets.Count(packet => packet.Data == "same-data"));
            }
        }

        [TestMethod]
        public void DifferentPayloadsFromSameSenderAreRetained()
        {
            int port;
            using (UdpBroadcastListener listener = CreateListener(out port))
            using (Socket sender = CreateSender())
            {
                listener.StartListen();
                Send(sender, port, "first-data");
                Send(sender, port, "second-data");
                string barrier = "__hazel_broadcast_test_barrier_payloads";
                Send(sender, port, barrier);

                BroadcastPacket[] packets = ReceiveAfterBarriers(listener, barrier);

                Assert.AreEqual(3, packets.Length);
                Assert.AreEqual(1, packets.Count(packet => packet.Data == "first-data"));
                Assert.AreEqual(1, packets.Count(packet => packet.Data == "second-data"));
            }
        }

        [TestMethod]
        public void SamePayloadFromDifferentSendersIsRetained()
        {
            int port;
            using (UdpBroadcastListener listener = CreateListener(out port))
            using (Socket firstSender = CreateSender())
            using (Socket secondSender = CreateSender())
            {
                listener.StartListen();
                Send(firstSender, port, "same-data");
                Send(secondSender, port, "same-data");
                string firstBarrier = "__hazel_broadcast_test_barrier_sender_one";
                string secondBarrier = "__hazel_broadcast_test_barrier_sender_two";
                Send(firstSender, port, firstBarrier);
                Send(secondSender, port, secondBarrier);

                BroadcastPacket[] packets = ReceiveAfterBarriers(listener, firstBarrier, secondBarrier);

                Assert.AreEqual(4, packets.Length);
                BroadcastPacket[] samePackets = packets.Where(packet => packet.Data == "same-data").ToArray();
                Assert.AreEqual(2, samePackets.Length);
                Assert.AreNotEqual(samePackets[0].Sender, samePackets[1].Sender);
            }
        }

        [TestMethod]
        public void GetPacketsDrainsQueueUntilTheNextDatagram()
        {
            int port;
            using (UdpBroadcastListener listener = CreateListener(out port))
            using (Socket sender = CreateSender())
            {
                listener.StartListen();
                string firstBarrier = "__hazel_broadcast_test_barrier_drain_one";
                Send(sender, port, "drain-data");
                Send(sender, port, firstBarrier);

                BroadcastPacket[] firstBatch = ReceiveAfterBarriers(listener, firstBarrier);
                Assert.AreEqual(2, firstBatch.Length);
                Assert.AreEqual(0, listener.GetPackets().Length);

                string secondBarrier = "__hazel_broadcast_test_barrier_drain_two";
                Send(sender, port, "after-drain");
                Send(sender, port, secondBarrier);

                BroadcastPacket[] secondBatch = ReceiveAfterBarriers(listener, secondBarrier);
                Assert.AreEqual(2, secondBatch.Length);
                Assert.AreEqual(1, secondBatch.Count(packet => packet.Data == "after-drain"));
            }
        }

        private static UdpBroadcastListener CreateListener(out int port)
        {
            UdpBroadcastListener listener = new UdpBroadcastListener(0);
            FieldInfo socketField = typeof(UdpBroadcastListener).GetField("socket", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(socketField);
            Socket socket = (Socket)socketField.GetValue(listener);
            port = ((IPEndPoint)socket.LocalEndPoint).Port;
            return listener;
        }

        private static Socket CreateSender()
        {
            Socket sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sender.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return sender;
        }

        private static void Send(Socket sender, int port, string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes("\x04\x02" + data);
            sender.SendTo(bytes, new IPEndPoint(IPAddress.Loopback, port));
        }

        private static List<BroadcastPacket> WaitForBufferedPackets(UdpBroadcastListener listener, params string[] barriers)
        {
            FieldInfo packetsField = typeof(UdpBroadcastListener).GetField("packets", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(packetsField);
            var packets = (List<BroadcastPacket>)packetsField.GetValue(listener);
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);

            while (DateTime.UtcNow < deadline)
            {
                lock (packets)
                {
                    if (ContainsAllBarriers(packets, barriers))
                    {
                        return packets;
                    }
                }

                Thread.Yield();
            }

            Assert.Fail("Timed out waiting for broadcast receipt barrier.");
            return packets;
        }

        private static BroadcastPacket[] ReceiveAfterBarriers(UdpBroadcastListener listener, params string[] barriers)
        {
            WaitForBufferedPackets(listener, barriers);
            return listener.GetPackets();
        }

        private static bool ContainsAllBarriers(List<BroadcastPacket> packets, string[] barriers)
        {
            foreach (string barrier in barriers)
            {
                if (!packets.Any(packet => packet.Data == barrier))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
