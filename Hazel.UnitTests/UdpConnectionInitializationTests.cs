using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Hazel.Udp;
using Hazel.Udp.FewerThreads;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class UdpConnectionInitializationTests
    {
        private const int TimeoutMs = 5000;

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void HelloRetriesWaitForInitializationWithoutBlockingOtherClients(bool threadLimited)
        {
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            using (var finished = new ManualResetEvent(false))
            using (var received = new ManualResetEvent(false))
            using (var listener = CreateListener(threadLimited))
            {
                int deliveries = 0;
                byte payload = 0;
                listener.NewConnection += e =>
                {
                    ((UdpConnection)e.Connection).KeepAliveInterval = Timeout.Infinite;
                    if (e.HandshakeData.ReadByte() != 41) return;

                    entered.Set();
                    try
                    {
                        if (!release.WaitOne(TimeoutMs * 2)) return;
                        e.Connection.DataReceived += data =>
                        {
                            payload = data.Message.ReadByte();
                            data.Message.Recycle();
                            Interlocked.Increment(ref deliveries);
                            received.Set();
                        };
                    }
                    finally
                    {
                        finished.Set();
                    }
                };

                listener.Start();
                var endpoint = GetBoundEndPoint(listener);
                using (var first = new UdpClientConnection(new TestLogger(), endpoint))
                using (var second = new UdpClientConnection(new TestLogger(), endpoint))
                {
                    first.ResendTimeoutMs = 50;
                    first.ResendPingMultiplier = 1;
                    first.DisconnectTimeoutMs = TimeoutMs * 2;
                    first.KeepAliveInterval = second.KeepAliveInterval = Timeout.Infinite;
                    try
                    {
                        first.ConnectAsync(new byte[] { 41 });
                        Assert.IsTrue(entered.WaitOne(TimeoutMs), "First initialization callback did not start.");

                        // With two workers, one remains available even while this client retries Hello.
                        second.Connect(new byte[] { 42 }, TimeoutMs);
                        Assert.AreEqual(ConnectionState.Connected, second.State);
                        Assert.IsFalse(first.WaitOnConnect(600), "A retried Hello was ACKed before handlers were installed.");
                        Assert.AreEqual(ConnectionState.Connecting, first.State);
                        Assert.IsTrue(first.Statistics.MessagesResent > 0, "The test must exercise Hello retransmission.");

                        release.Set();
                        Assert.IsTrue(first.WaitOnConnect(TimeoutMs), "Hello was not ACKed after initialization.");
                        Assert.AreEqual(ConnectionState.Connected, first.State);
                        var message = MessageWriter.Get(SendOption.Reliable);
                        try
                        {
                            message.Write((byte)73);
                            Assert.AreEqual(SendErrors.None, first.Send(message));
                        }
                        finally
                        {
                            message.Recycle();
                        }

                        Assert.IsTrue(received.WaitOne(TimeoutMs), "The first reliable message was lost.");
                        Assert.IsTrue(SpinWait.SpinUntil(() => first.Statistics.ReliablePacketsAcknowledged >= 2, TimeoutMs));
                        Assert.AreEqual((byte)73, payload);
                        Assert.AreEqual(1, Volatile.Read(ref deliveries));
                    }
                    finally
                    {
                        ReleaseCallback(entered, release, finished);
                    }
                }
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ReliablePacketsDuringInitializationCanBeRetriedAfterHandlersAreInstalled(bool threadLimited)
        {
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            using (var finished = new ManualResetEvent(false))
            using (var received = new ManualResetEvent(false))
            using (var listener = CreateListener(threadLimited))
            using (var client = CreateRawClient())
            {
                int deliveries = 0;
                byte payload = 0;
                UdpConnection server = null;
                listener.NewConnection += e =>
                {
                    server = (UdpConnection)e.Connection;
                    server.KeepAliveInterval = Timeout.Infinite;
                    entered.Set();
                    try
                    {
                        if (!release.WaitOne(TimeoutMs * 2)) return;
                        e.Connection.DataReceived += data =>
                        {
                            payload = data.Message.ReadByte();
                            data.Message.Recycle();
                            Interlocked.Increment(ref deliveries);
                            received.Set();
                        };
                    }
                    finally
                    {
                        finished.Set();
                    }
                };

                listener.Start();
                var endpoint = GetBoundEndPoint(listener);
                var hello = new byte[] { (byte)UdpSendOption.Hello, 0, 0, 0 };
                var reliable = new byte[] { (byte)SendOption.Reliable, 0, 1, 73 };
                try
                {
                    client.SendTo(hello, endpoint);
                    Assert.IsTrue(entered.WaitOne(TimeoutMs));
                    for (int i = 0; i < 3; ++i)
                    {
                        client.SendTo(hello, endpoint);
                        client.SendTo(reliable, endpoint);
                    }
                    client.SendTo(new byte[] { (byte)UdpSendOption.Ping, 0, 2 }, endpoint);
                    Assert.IsFalse(client.Poll(500000, SelectMode.SelectRead), "Initializing traffic must remain unACKed.");

                    release.Set();
                    WaitForAck(client, 0);
                    client.SendTo(reliable, endpoint);
                    WaitForAck(client, 1);
                    Assert.IsTrue(received.WaitOne(TimeoutMs), "An early packet consumed the reliable receive state.");
                    Assert.IsTrue(SpinWait.SpinUntil(() => server.Statistics.ReliableMessagesReceived == 1, TimeoutMs));
                    client.SendTo(reliable, endpoint);
                    WaitForAck(client, 1);
                    // ACKs precede dispatch; wait for the duplicate's receive processing to finish.
                    Assert.IsTrue(SpinWait.SpinUntil(() => server.Statistics.ReliableMessagesReceived == 2, TimeoutMs));
                    Assert.AreEqual((byte)73, payload);
                    Assert.AreEqual(1, Volatile.Read(ref deliveries), "A reliable retry was delivered more than once.");
                }
                finally
                {
                    ReleaseCallback(entered, release, finished);
                }
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void InitializationCallbackCanWaitForItsOutboundReliableAcknowledgement(bool threadLimited)
        {
            using (var finished = new ManualResetEvent(false))
            using (var listener = CreateListener(threadLimited))
            using (var client = CreateRawClient())
            {
                bool acknowledged = false;
                SendErrors sendResult = SendErrors.Unknown;
                listener.NewConnection += e =>
                {
                    var connection = (UdpConnection)e.Connection;
                    connection.KeepAliveInterval = Timeout.Infinite;
                    var message = MessageWriter.Get(SendOption.Reliable);
                    try
                    {
                        message.Write((byte)73);
                        sendResult = connection.Send(message);
                        acknowledged = SpinWait.SpinUntil(() => connection.Statistics.ReliablePacketsAcknowledged == 1, TimeoutMs);
                    }
                    finally
                    {
                        message.Recycle();
                        finished.Set();
                    }
                };

                listener.Start();
                var endpoint = GetBoundEndPoint(listener);
                client.SendTo(new byte[] { (byte)UdpSendOption.Hello, 0, 0, 0 }, endpoint);
                try
                {
                    var outbound = ReceivePacket(client, TimeoutMs);
                    Assert.AreEqual((byte)SendOption.Reliable, outbound[0]);
                    Assert.AreEqual((byte)73, outbound[3]);
                    client.SendTo(new byte[] { (byte)UdpSendOption.Acknowledgement, outbound[1], outbound[2] }, endpoint);
                    Assert.IsTrue(finished.WaitOne(TimeoutMs * 2), "Initialization callback did not finish.");
                    Assert.AreEqual(SendErrors.None, sendResult);
                    Assert.IsTrue(acknowledged, "An incoming ACK must progress while NewConnection is still executing.");
                    WaitForAck(client, 0);
                }
                finally
                {
                    finished.WaitOne(TimeoutMs * 2);
                }
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void RemoteDisconnectDuringInitializationPreventsHelloAcknowledgement(bool threadLimited)
        {
            using (var entered = new ManualResetEvent(false))
            using (var release = new ManualResetEvent(false))
            using (var finished = new ManualResetEvent(false))
            using (var disconnected = new ManualResetEvent(false))
            using (var listener = CreateListener(threadLimited))
            using (var client = CreateRawClient())
            {
                listener.NewConnection += e =>
                {
                    ((UdpConnection)e.Connection).KeepAliveInterval = Timeout.Infinite;
                    e.Connection.Disconnected += (sender, args) => disconnected.Set();
                    entered.Set();
                    try
                    {
                        release.WaitOne(TimeoutMs * 2);
                    }
                    finally
                    {
                        finished.Set();
                    }
                };

                listener.Start();
                var endpoint = GetBoundEndPoint(listener);
                try
                {
                    client.SendTo(new byte[] { (byte)UdpSendOption.Hello, 0, 0, 0 }, endpoint);
                    Assert.IsTrue(entered.WaitOne(TimeoutMs));
                    client.SendTo(new byte[] { (byte)UdpSendOption.Disconnect }, endpoint);
                    Assert.IsTrue(disconnected.WaitOne(TimeoutMs), "Disconnect must progress during initialization.");
                    Assert.AreEqual((byte)UdpSendOption.Disconnect, ReceivePacket(client, TimeoutMs)[0]);
                    release.Set();
                    Assert.IsTrue(finished.WaitOne(TimeoutMs));
                    Assert.IsFalse(client.Poll(500000, SelectMode.SelectRead), "A closed connection must not ACK the original Hello.");
                    Assert.AreEqual(0, listener.ConnectionCount);
                }
                finally
                {
                    ReleaseCallback(entered, release, finished);
                }
            }
        }

        private static NetworkConnectionListener CreateListener(bool threadLimited)
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);
            return threadLimited
                ? (NetworkConnectionListener)new ThreadLimitedUdpConnectionListener(2, endpoint, new TestLogger())
                : new UdpConnectionListener(endpoint, logger: new TestLogger());
        }

        private static IPEndPoint GetBoundEndPoint(NetworkConnectionListener listener)
        {
            // EndPoint retains port zero, so inspect only the bound socket to discover its ephemeral port.
            var socket = (Socket)listener.GetType().GetField("socket", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(listener);
            return (IPEndPoint)socket.LocalEndPoint;
        }

        private static Socket CreateRawClient()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return socket;
        }

        private static byte[] ReceivePacket(Socket socket, int timeoutMs)
        {
            Assert.IsTrue(socket.Poll(timeoutMs * 1000, SelectMode.SelectRead), "Timed out waiting for a UDP packet.");
            var buffer = new byte[1024];
            int length = socket.Receive(buffer);
            Array.Resize(ref buffer, length);
            return buffer;
        }

        private static void WaitForAck(Socket socket, ushort id)
        {
            var timeout = Stopwatch.StartNew();
            while (timeout.ElapsedMilliseconds < TimeoutMs)
            {
                var packet = ReceivePacket(socket, Math.Max(1, TimeoutMs - (int)timeout.ElapsedMilliseconds));
                if (packet.Length >= 3 && packet[0] == (byte)UdpSendOption.Acknowledgement
                    && packet[1] == (byte)(id >> 8) && packet[2] == (byte)id)
                {
                    return;
                }
            }

            Assert.Fail("Timed out waiting for ACK " + id + ".");
        }

        private static void ReleaseCallback(ManualResetEvent entered, ManualResetEvent release, ManualResetEvent finished)
        {
            release.Set();
            if (entered.WaitOne(0)) finished.WaitOne(TimeoutMs);
        }
    }
}
