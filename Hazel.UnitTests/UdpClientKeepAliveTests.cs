using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Hazel.Udp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class UdpClientKeepAliveTests
    {
        private const int TimeoutMs = 5000;

        [DataTestMethod]
        [DataRow(Timeout.Infinite)]
        [DataRow(10000)]
        public void IntervalChangesAfterHelloAcknowledgementControlAllKeepAlives(int interval)
        {
            using (var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var connection = new UdpClientConnection(new TestLogger(), (IPEndPoint)peer.Client.LocalEndPoint))
            {
                // Keep a potentially orphaned timer reachable so GC timing cannot hide the regression.
                var originalTimer = GetKeepAliveTimer(connection);
                try
                {
                    connection.KeepAliveInterval = 50;
                    var client = CompleteHandshake(peer, connection);

                    var ping = Receive(peer, ref client);
                    Assert.AreEqual((byte)UdpSendOption.Ping, ping[0], "The connected client must send keepalives.");
                    Assert.AreEqual(3, ping.Length);
                    Acknowledge(peer, client, ping);
                    Assert.IsTrue(SpinWait.SpinUntil(() => connection.Statistics.AcknowledgementMessagesReceived >= 2, TimeoutMs));

                    connection.KeepAliveInterval = interval;
                    // Timer.Change does not cancel callbacks that are already queued. Allow those to finish,
                    // then discard their datagrams before checking for further keepalives.
                    Thread.Sleep(150);
                    while (peer.Client.Poll(0, SelectMode.SelectRead)) Receive(peer, ref client);

                    Assert.IsFalse(peer.Client.Poll(300000, SelectMode.SelectRead),
                        "The previous keepalive interval remained active after it was changed.");
                    Assert.AreEqual(ConnectionState.Connected, connection.State);

                    connection.KeepAliveInterval = 50;
                    ping = Receive(peer, ref client);
                    Assert.AreEqual((byte)UdpSendOption.Ping, ping[0], "Keepalives must resume when the interval is enabled again.");
                }
                finally
                {
                    GC.KeepAlive(originalTimer);
                    // Dispose an orphan too when running this regression against the unfixed code.
                    originalTimer.Dispose();
                }
            }
        }

        [TestMethod]
        public void DisposeAfterHelloAcknowledgementDisposesTheOriginalKeepAliveTimer()
        {
            using (var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var connection = new UdpClientConnection(new TestLogger(), (IPEndPoint)peer.Client.LocalEndPoint))
            {
                var originalTimer = GetKeepAliveTimer(connection);
                try
                {
                    connection.KeepAliveInterval = Timeout.Infinite;
                    CompleteHandshake(peer, connection);
                    connection.Dispose();

                    Assert.ThrowsException<ObjectDisposedException>(() => originalTimer.Change(Timeout.Infinite, Timeout.Infinite),
                        "Disposing the connection must release the timer created by its constructor.");
                }
                finally
                {
                    originalTimer.Dispose();
                }
            }
        }

        private static IPEndPoint CompleteHandshake(UdpClient peer, UdpClientConnection connection)
        {
            connection.ConnectAsync();
            IPEndPoint client = null;
            var hello = Receive(peer, ref client);
            Assert.AreEqual((byte)UdpSendOption.Hello, hello[0]);
            Acknowledge(peer, client, hello);

            // The acknowledgement counter is updated after the Hello callback completes.
            Assert.IsTrue(SpinWait.SpinUntil(() => connection.Statistics.AcknowledgementMessagesReceived == 1, TimeoutMs),
                "The client did not finish processing the Hello acknowledgement.");
            Assert.AreEqual(ConnectionState.Connected, connection.State);
            return client;
        }

        private static byte[] Receive(UdpClient peer, ref IPEndPoint sender)
        {
            peer.Client.ReceiveTimeout = TimeoutMs;
            return peer.Receive(ref sender);
        }

        private static void Acknowledge(UdpClient peer, IPEndPoint client, byte[] packet)
        {
            var acknowledgement = new byte[] { (byte)UdpSendOption.Acknowledgement, packet[1], packet[2] };
            peer.Send(acknowledgement, acknowledgement.Length, client);
        }

        private static Timer GetKeepAliveTimer(UdpClientConnection connection)
        {
            return (Timer)typeof(UdpConnection).GetField("keepAliveTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(connection);
        }
    }
}
