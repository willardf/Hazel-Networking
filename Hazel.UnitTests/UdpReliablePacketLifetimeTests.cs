using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Hazel.Udp;
using Hazel.Udp.FewerThreads;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class UdpReliablePacketLifetimeTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ResendRetainsBufferDuringAcknowledgementOrDispose(bool dispose)
        {
            using (var connection = new LifetimeTestConnection())
            using (var entered = new ManualResetEventSlim())
            using (var resume = new ManualResetEventSlim())
            {
                connection.Connect();
                connection.SendReliable(0x11);
                var expected = connection.BytesSent[0].Buffer.Take(4).ToArray();
                connection.reliableDataPacketsSent.Values.Single().Packet.NextTimeoutMs = 0;

                int pause = 1;
                connection.BeforeWrite = () =>
                {
                    if (Interlocked.Exchange(ref pause, 0) == 1)
                    {
                        entered.Set();
                        Assert.IsTrue(resume.Wait(5000), "Timed out waiting to resume the transport.");
                    }
                };

                Task<int> resend = Task.Run(() => connection.ManageReliablePackets());
                try
                {
                    Assert.IsTrue(entered.Wait(5000), "The resend did not reach the transport.");
                    if (dispose)
                    {
                        connection.Dispose();
                    }
                    else
                    {
                        connection.Acknowledge(expected);
                    }

                    // Exercise the same connection's pools while the old write is suspended.
                    connection.SendUnreliable(0x22, 0x33, 0x44);
                    connection.SendReliable(0x55);
                    Assert.AreEqual(1, connection.PacketPool.NumberCreated, "The retired Packet should be reused.");
                }
                finally
                {
                    resume.Set();
                    Assert.IsTrue(resend.Wait(5000), "The resend did not finish.");
                }

                Assert.AreEqual(1, resend.Result);
                CollectionAssert.AreEqual(expected, connection.BytesSent.Last().Buffer.Take(4).ToArray());
                connection.Dispose();
                Assert.AreEqual(0, connection.BuffersInUse);
                Assert.AreEqual(0, connection.PacketPool.NumberInUse);
            }
        }

        [TestMethod]
        public void ResendReleasesBufferWhenTransportThrowsAfterAcknowledgement()
        {
            using (var connection = new LifetimeTestConnection())
            {
                connection.Connect();
                connection.SendReliable(0x11);
                var expected = connection.BytesSent[0].Buffer.Take(4).ToArray();
                connection.reliableDataPacketsSent.Values.Single().Packet.NextTimeoutMs = 0;
                connection.BeforeWrite = () =>
                {
                    connection.Acknowledge(expected);
                    throw new InvalidOperationException("Simulated closed transport.");
                };

                Assert.AreEqual(0, connection.ManageReliablePackets());
                Assert.AreEqual(0, connection.BuffersInUse);
                Assert.AreEqual(0, connection.PacketPool.NumberInUse);
                Assert.AreEqual(ConnectionState.NotConnected, connection.State);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void StaleRegistrationCannotResendOrRecycleReusedPacket(bool reuseId)
        {
            using (var connection = new LifetimeTestConnection())
            {
                connection.Connect();
                connection.SendReliable(0x11);
                var retired = connection.reliableDataPacketsSent.Values.Single();
                connection.Acknowledge(connection.BytesSent[0].Buffer);

                if (reuseId)
                {
                    // Simulate ushort ID wraparound without sending 65,536 unrelated packets.
                    typeof(UdpConnection).GetField("lastIDAllocated", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(connection, -1);
                }

                connection.SendReliable(0x55);
                var current = connection.reliableDataPacketsSent.Values.Single();
                Assert.AreSame(retired.Packet, current.Packet);
                Assert.AreNotEqual(retired.Generation, current.Generation);
                Assert.AreEqual(reuseId, connection.BytesSent[0].Buffer[2] == connection.BytesSent[1].Buffer[2]);
                current.Packet.NextTimeoutMs = 0;

                Assert.AreEqual(0, retired.Packet.Resend(retired.Generation));
                connection.DisconnectTimeoutMs = 0;
                Assert.AreEqual(0, retired.Packet.Resend(retired.Generation));
                retired.Packet.Recycle(retired.Generation);

                Assert.AreEqual(2, connection.BytesSent.Count);
                Assert.AreEqual(ConnectionState.Connected, connection.State);
                Assert.AreEqual(current, connection.reliableDataPacketsSent.Values.Single());
                Assert.AreEqual(1, connection.BuffersInUse);
                connection.Dispose();
                Assert.AreEqual(0, connection.BuffersInUse);
                Assert.AreEqual(0, connection.PacketPool.NumberInUse);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TimeoutRecyclesBeforeReentrantDisconnectCallback(bool resendLimit)
        {
            using (var connection = new LifetimeTestConnection())
            {
                connection.Connect();
                connection.SendReliable(0x11);
                var packet = connection.reliableDataPacketsSent.Values.Single().Packet;
                packet.NextTimeoutMs = 0;
                if (resendLimit)
                {
                    connection.ResendLimit = 1;
                    packet.Retransmissions = 1;
                }
                else
                {
                    connection.DisconnectTimeoutMs = 0;
                }

                bool callbackInvoked = false;
                connection.OnInternalDisconnect = error =>
                {
                    Assert.AreEqual(0, connection.PacketPool.NumberInUse);
                    Assert.AreEqual(0, connection.BuffersInUse);
                    // Reuse the retired Packet from another thread while the callback is still active.
                    Task send = Task.Run(() => connection.SendReliable(0x55));
                    Assert.IsTrue(send.Wait(5000), "The callback must run outside the Packet lifecycle lock.");
                    callbackInvoked = true;
                    return null;
                };

                Assert.AreEqual(0, connection.ManageReliablePackets());
                Assert.IsTrue(callbackInvoked);
                Assert.AreEqual(1, connection.PacketPool.NumberCreated);
                Assert.AreEqual(0, connection.PacketPool.NumberInUse);
                Assert.AreEqual(0, connection.BuffersInUse);
                Assert.AreEqual(0, connection.reliableDataPacketsSent.Count);
            }
        }

        [TestMethod]
        public void ThrowingAcknowledgementCallbackStillRecyclesPacket()
        {
            using (var connection = new LifetimeTestConnection())
            {
                connection.Connect();
                connection.SendReliable(0x11, () => throw new InvalidOperationException("Simulated callback failure."));
                Assert.ThrowsException<InvalidOperationException>(() => connection.Acknowledge(connection.BytesSent[0].Buffer));
                Assert.AreEqual(0, connection.PacketPool.NumberInUse);
                Assert.AreEqual(0, connection.BuffersInUse);
            }
        }

        [TestMethod]
        public void DisposedClientSendDoesNotRetainTransportBuffer()
        {
            using (var client = new UdpClientConnection(new TestLogger(), new IPEndPoint(IPAddress.Loopback, 1)))
            {
                client.Dispose();
                AssertBufferReturned(buffer =>
                    typeof(UdpClientConnection).GetMethod("WriteBytesToConnection", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(client, new object[] { buffer, buffer.Length }));
            }
        }

        [TestMethod]
        public void DisposedListenerSendDoesNotRetainTransportBuffer()
        {
            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                listener.Dispose();
                AssertBufferReturned(buffer => listener.SendData(buffer, buffer.Length, new IPEndPoint(IPAddress.Loopback, 1)));
            }
        }

        [TestMethod]
        public void ClosedListenerQueueDoesNotRetainTransportBuffer()
        {
            using (var listener = new ThreadLimitedUdpConnectionListener(1, new IPEndPoint(IPAddress.Loopback, 0), new TestLogger()))
            {
                // CompleteAdding represents the shutdown window before the queue is disposed.
                var queue = typeof(ThreadLimitedUdpConnectionListener)
                    .GetField("sendQueue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(listener);
                queue.GetType().GetMethod("CompleteAdding").Invoke(queue, null);
                AssertBufferReturned(buffer => Assert.ThrowsException<InvalidOperationException>(() =>
                    listener.SendDataRaw(buffer, new IPEndPoint(IPAddress.Loopback, 1))));
            }
        }

        private static void AssertBufferReturned(Action<SmartBuffer> send)
        {
            ObjectPool<SmartBuffer> pool = null;
            pool = new ObjectPool<SmartBuffer>(() => new SmartBuffer(pool, 4));
            using (var buffer = pool.GetObject())
            {
                buffer.Length = 4;
                send(buffer);
            }

            Assert.AreEqual(0, pool.NumberInUse, "A failed transport handoff retained a buffer usage.");
        }

#if DEBUG
        [TestMethod]
        public void UdpClientResendRetainsWireBytesAcrossRealAcknowledgement()
        {
            using (var receiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            using (var entered = new ManualResetEventSlim())
            using (var resume = new ManualResetEventSlim())
            {
                receiver.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                receiver.ReceiveTimeout = 5000;
                using (var client = new UdpClientConnection(new TestLogger(), (IPEndPoint)receiver.LocalEndPoint))
                {
                    client.KeepAliveInterval = Timeout.Infinite;
                    client.ResendTimeoutMs = 1;
                    client.ConnectAsync();
                    EndPoint sender;
                    byte[] hello = ReceiveDatagram(receiver, out sender);
                    receiver.SendTo(new[] { (byte)10, hello[1], hello[2] }, sender);
                    Assert.IsTrue(client.WaitOnConnect(5000));

                    var message = MessageWriter.Get(SendOption.Reliable);
                    message.Write((byte)0x11);
                    Assert.AreEqual(SendErrors.None, client.Send(message));
                    message.Recycle();
                    byte[] expected = ReceiveDatagram(receiver, out sender);
                    Assert.AreEqual((byte)SendOption.Reliable, expected[0]);

                    int pause = 1;
                    byte[] suspendedBuffer = null;
                    client.DataSentRaw += (bytes, length) =>
                    {
                        if (bytes[0] == (byte)SendOption.Reliable && Interlocked.Exchange(ref pause, 0) == 1)
                        {
                            suspendedBuffer = bytes;
                            entered.Set();
                            Assert.IsTrue(resume.Wait(5000));
                        }
                    };

                    try
                    {
                        Assert.IsTrue(entered.Wait(5000), "The client's resend timer did not reach the transport.");
                        while (receiver.Poll(0, SelectMode.SelectRead))
                        {
                            ReceiveDatagram(receiver, out sender);
                        }

                        receiver.SendTo(new[] { (byte)10, expected[1], expected[2] }, sender);
                        Assert.IsTrue(SpinWait.SpinUntil(() => client.Statistics.ReliablePacketsAcknowledged == 2, 5000));
                        message = MessageWriter.Get(SendOption.None);
                        message.Write(new byte[] { 0x22, 0x33, 0x44 });
                        Assert.AreEqual(SendErrors.None, client.Send(message));
                        message.Recycle();
                        CollectionAssert.AreEqual(new byte[] { 0, 0x22, 0x33, 0x44 }, ReceiveDatagram(receiver, out sender));
                        CollectionAssert.AreEqual(expected, suspendedBuffer.Take(expected.Length).ToArray());
                    }
                    finally
                    {
                        resume.Set();
                    }

                    CollectionAssert.AreEqual(expected, ReceiveDatagram(receiver, out sender));
                }
            }
        }

        private static byte[] ReceiveDatagram(Socket socket, out EndPoint sender)
        {
            sender = new IPEndPoint(IPAddress.Any, 0);
            var bytes = new byte[64];
            int length = socket.ReceiveFrom(bytes, ref sender);
            return bytes.Take(length).ToArray();
        }
#endif

        private sealed class LifetimeTestConnection : UdpConnectionTestHarness
        {
            public Action BeforeWrite;
            public int BuffersInUse => this.bufferPool.NumberInUse;

            public LifetimeTestConnection()
            {
                this.DisconnectTimeoutMs = int.MaxValue;
            }

            public void SendReliable(byte payload, Action acknowledged = null)
            {
                this.HandleSend(new[] { payload }, (byte)SendOption.Reliable, acknowledged);
            }

            public void SendUnreliable(params byte[] payload)
            {
                this.HandleSend(payload, (byte)SendOption.None);
            }

            public void Acknowledge(byte[] packet)
            {
                var message = MessageReader.Get(new[] { (byte)10, packet[1], packet[2] });
                try
                {
                    this.HandleReceive(message, message.Length);
                }
                catch
                {
                    message.Recycle();
                    throw;
                }
            }

            protected override void WriteBytesToConnection(SmartBuffer bytes, int length)
            {
                this.BeforeWrite?.Invoke();
                base.WriteBytesToConnection(bytes, length);
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                foreach (var message in this.BytesSent)
                {
                    message.Recycle();
                }

                this.BytesSent.Clear();
            }
        }
    }
}
