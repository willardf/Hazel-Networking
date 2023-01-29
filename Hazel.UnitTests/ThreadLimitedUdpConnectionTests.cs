using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading;
using Hazel.Udp;
using Hazel.Udp.FewerThreads;
using System.Net.Sockets;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace Hazel.UnitTests
{
    [TestClass]
    public class ThreadLimitedUdpConnectionTests
    {
        protected ThreadLimitedUdpConnectionListener CreateListener(int numWorkers, IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            return new ThreadLimitedUdpConnectionListener(numWorkers, endPoint, logger, ipMode);
        }

        protected UdpConnection CreateConnection(IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            return new UdpClientConnection(logger, endPoint, ipMode);
        }

        [TestMethod]
        public void ServerDisposeDisconnectsTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("SERVER")))
            using (UdpConnection connection = this.CreateConnection(ep, new TestLogger("CLIENT")))
            {
                listener.NewConnection += (evt) =>
                {
                    serverConnected = true;
                    evt.Connection.Disconnected += (o, et) => serverDisconnected = true;
                };
                connection.Disconnected += (o, evt) => clientDisconnected = true;

                listener.Start();
                connection.Connect();

                Thread.Sleep(100); // Gotta wait for the server to set up the events.
                listener.Dispose();
                Thread.Sleep(100);

                Assert.IsTrue(serverConnected);
                Assert.IsTrue(clientDisconnected);
                Assert.IsFalse(serverDisconnected);
            }
        }

        [TestMethod]
        public void ClientDisposeDisconnectTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(ep, new TestLogger()))
            {
                listener.NewConnection += (evt) =>
                {
                    serverConnected = true;
                    evt.Connection.Disconnected += (o, et) => serverDisconnected = true;
                };

                connection.Disconnected += (o, et) => clientDisconnected = true;

                listener.Start();
                connection.Connect();

                Thread.Sleep(100); // Gotta wait for the server to set up the events.
                connection.Dispose();

                Thread.Sleep(100);

                Assert.IsTrue(serverConnected);
                Assert.IsTrue(serverDisconnected);
                Assert.IsFalse(clientDisconnected);
            }
        }

        /// <summary>
        ///     Tests the fields on UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpFieldTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(ep, new TestLogger()))
            {
                listener.Start();

                connection.Connect();

                //Connection fields
                Assert.AreEqual(ep, connection.EndPoint);

                //UdpConnection fields
                Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 4296), connection.EndPoint);
                Assert.AreEqual(1, connection.Statistics.DataBytesSent);
                Assert.AreEqual(0, connection.Statistics.DataBytesReceived);
            }
        }

        [TestMethod]
        public void UdpHandshakeTest()
        {
            byte[] TestData = new byte[] { 1, 2, 3, 4, 5, 6 };
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                listener.Start();

                MessageReader output = null;
                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    output = e.HandshakeData.Duplicate();
                };

                connection.Connect(TestData);

                Thread.Sleep(10);
                for (int i = 0; i < TestData.Length; ++i)
                {
                    Assert.AreEqual(TestData[i], output.ReadByte());
                }
            }
        }

        [TestMethod]
        public void UdpUnreliableMessageSendTest()
        {
            byte[] TestData = new byte[] { 1, 2, 3, 4, 5, 6 };
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                MessageReader output = null;
                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    e.Connection.DataReceived += delegate (DataReceivedEventArgs evt)
                    {
                        output = evt.Message.Duplicate();
                    };
                };

                listener.Start();
                connection.Connect();

                for (int i = 0; i < 4; ++i)
                {
                    var msg = MessageWriter.Get(SendOption.None);
                    msg.Write(TestData);
                    connection.Send(msg);
                    msg.Recycle();
                }

                Thread.Sleep(10);
                for (int i = 0; i < TestData.Length; ++i)
                {
                    Assert.AreEqual(TestData[i], output.ReadByte());
                }
            }
        }

        [TestMethod]
        public void UdpReliableMessageResendTest()
        {
            byte[] TestData = new byte[] { 1, 2, 3, 4, 5, 6 };

            var listenerEp = new IPEndPoint(IPAddress.Loopback, 4296);
            var captureEp = new IPEndPoint(IPAddress.Loopback, 4297);

            using (SocketCapture capture = new SocketCapture(captureEp, listenerEp, new TestLogger()))
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, listenerEp.Port), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(captureEp, new TestLogger()))
            using (SemaphoreSlim readLock = new SemaphoreSlim(0, 1))
            {
                connection.ResendTimeoutMs = 100;
                connection.KeepAliveInterval = Timeout.Infinite; // Don't let pings interfere.

                MessageReader output = null;
                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    var udpConn = (UdpConnection)e.Connection;
                    udpConn.KeepAliveInterval = Timeout.Infinite; // Don't let pings interfere.
                    
                    e.Connection.DataReceived += delegate (DataReceivedEventArgs evt)
                    {
                        output = evt.Message.Duplicate();
                        readLock.Release();
                    };
                };

                listener.Start();
                connection.Connect();

                capture.AssertPacketsToRemoteCountEquals(0);

                const int NumberOfPacketsToResend = 4;
                const int NumberOfTimesToResend = 3;
                using (capture.SendToRemoteSemaphore = new Semaphore(0, int.MaxValue))
                {
                    for (int pktCnt = 0; pktCnt < NumberOfPacketsToResend; ++pktCnt)
                    {
                        Console.WriteLine("Send blocked pkt");
                        var msg = MessageWriter.Get(SendOption.Reliable);
                        msg.Write(TestData);
                        connection.Send(msg);
                        msg.Recycle();

                        for (int drops = 0; drops < NumberOfTimesToResend; ++drops)
                        {
                            capture.AssertPacketsToRemoteCountEquals(1);
                            capture.DiscardPacketForRemote();
                        }

                        capture.AssertPacketsToRemoteCountEquals(1);
                        capture.SendToRemoteSemaphore.Release(); // Actually let it send.

                        Assert.IsTrue(readLock.Wait(1000));
                        for (int i = 0; i < TestData.Length; ++i)
                        {
                            Assert.AreEqual(TestData[i], output.ReadByte());
                        }

                        output = null;
                    }
                }

                Assert.AreEqual(NumberOfPacketsToResend * NumberOfTimesToResend, connection.Statistics.MessagesResent);
            }
        }

        [TestMethod]
        public void UdpReliableMessageAckTest()
        {
            byte[] TestData = new byte[] { 1, 2, 3, 4, 5, 6 };

            var listenerEp = new IPEndPoint(IPAddress.Loopback, 4296);
            var captureEp = new IPEndPoint(IPAddress.Loopback, 4297);

            using (SocketCapture capture = new SocketCapture(captureEp, listenerEp, new TestLogger()))
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, listenerEp.Port), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(captureEp, new TestLogger()))
            {
                connection.ResendTimeoutMs = 100;
                connection.KeepAliveInterval = Timeout.Infinite; // Don't let pings interfere.

                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    var udpConn = (UdpConnection)e.Connection;
                    udpConn.KeepAliveInterval = Timeout.Infinite; // Don't let pings interfere.
                };

                listener.Start();
                connection.Connect();

                capture.AssertPacketsToLocalCountEquals(0);

                const int NumberOfPacketsToSend = 4;
                using (capture.SendToLocalSemaphore = new Semaphore(0, int.MaxValue))
                {
                    for (int pktCnt = 0; pktCnt < NumberOfPacketsToSend; ++pktCnt)
                    {
                        Console.WriteLine("Send blocked pkt");
                        var msg = MessageWriter.Get(SendOption.Reliable);
                        msg.Write(TestData);
                        connection.Send(msg);

                        msg.Recycle();

                        capture.AssertPacketsToLocalCountEquals(1);

                        var ack = capture.PeekPacketForLocal();
                        Assert.AreEqual(10, ack[0]); // enum SendOptionInternal.Acknowledgement
                        Assert.AreEqual(0, ack[1]);
                        Assert.AreEqual(pktCnt + 1, ack[2]);
                        Assert.AreEqual(255, ack[3]);

                        capture.SendToLocalSemaphore.Release(); // Actually let it send.
                        capture.AssertPacketsToLocalCountEquals(0);
                    }
                }

                // +1 for Hello packet
                Thread.Sleep(100); // The final ack has to actually be processed.
                Assert.AreEqual(1 + NumberOfPacketsToSend, connection.Statistics.ReliablePacketsAcknowledged);
            }
        }

        [TestMethod]
        public void UdpReliableMessageAckWithDropTest()
        {
            byte[] TestData = new byte[] { 1, 2, 3, 4, 5, 6 };

            var listenerEp = new IPEndPoint(IPAddress.Loopback, 4296);
            var captureEp = new IPEndPoint(IPAddress.Loopback, 4297);

            using (SocketCapture capture = new SocketCapture(captureEp, listenerEp, new TestLogger()))
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, listenerEp.Port), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(captureEp, new TestLogger()))
            {
                connection.ResendTimeoutMs = 10000; // No resends please
                connection.KeepAliveInterval = Timeout.Infinite; // Don't let pings interfere.

                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    var udpConn = (UdpConnection)e.Connection;
                    udpConn.KeepAliveInterval = Timeout.Infinite; // Don't let pings interfere.
                };

                listener.Start();
                connection.Connect();

                capture.AssertPacketsToLocalCountEquals(0);

                using (capture.SendToRemoteSemaphore = new Semaphore(0, int.MaxValue))
                using (capture.SendToLocalSemaphore = new Semaphore(0, int.MaxValue))
                {
                    // Send 3 packets to remote
                    for (int pktCnt = 0; pktCnt < 3; ++pktCnt)
                    {
                        var msg = MessageWriter.Get(SendOption.Reliable);
                        msg.Write(TestData);
                        connection.Send(msg);
                        msg.Recycle();
                    }

                    // Drop the middle packet
                    capture.AssertPacketsToRemoteCountEquals(3);
                    capture.ReorderPacketsForRemote(list => list.Sort(SortByPacketId.Instance));

                    capture.SendToRemoteSemaphore.Release();
                    capture.DiscardPacketForRemote();
                    capture.SendToRemoteSemaphore.Release();

                    // Receive 2 acks
                    capture.AssertPacketsToLocalCountEquals(2);
                    capture.ReorderPacketsForLocal(list => list.Sort(SortByPacketId.Instance));

                    var ack1 = capture.PeekPacketForLocal();
                    Assert.AreEqual(10, ack1[0]); // enum SendOptionInternal.Acknowledgement
                    Assert.AreEqual(0, ack1[1]);
                    Assert.AreEqual(1, ack1[2]);
                    Assert.AreEqual(255, ack1[3]);
                    capture.ReleasePacketsToLocal(1);

                    var ack2 = capture.PeekPacketForLocal();
                    Assert.AreEqual(10, ack2[0]); // enum SendOptionInternal.Acknowledgement
                    Assert.AreEqual(0, ack2[1]);
                    Assert.AreEqual(3, ack2[2]);
                    Assert.AreEqual(254, ack2[3]); // The server is expecting packet 2
                    capture.ReleasePacketsToLocal(1);
                }

                // +1 for Hello packet, +2 for reliable
                Thread.Sleep(100); // The final ack has to actually be processed.
                Assert.AreEqual(3, connection.Statistics.ReliablePacketsAcknowledged);
            }
        }

        [TestMethod]
        public void UdpReliableMessageAckFillsDroppedAcksTest()
        {
            byte[] TestData = new byte[] { 1, 2, 3, 4, 5, 6 };

            var listenerEp = new IPEndPoint(IPAddress.Loopback, 4296);
            var captureEp = new IPEndPoint(IPAddress.Loopback, 4297);

            using (SocketCapture capture = new SocketCapture(captureEp, listenerEp, new TestLogger()))
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, listenerEp.Port), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(captureEp, new TestLogger("Client")))
            {
                connection.ResendTimeoutMs = 10000; // No resends please
                connection.KeepAliveInterval = Timeout.Infinite; // Don't let pings interfere.

                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    var udpConn = (UdpConnection)e.Connection;
                    udpConn.KeepAliveInterval = Timeout.Infinite; // Don't let pings interfere.
                };

                listener.Start();
                connection.Connect();

                capture.AssertPacketsToLocalCountEquals(0);

                using (capture.SendToLocalSemaphore = new Semaphore(0, int.MaxValue))
                {
                    // Send 4 packets to remote
                    for (int pktCnt = 0; pktCnt < 4; ++pktCnt)
                    {
                        var msg = MessageWriter.Get(SendOption.Reliable);
                        msg.Write(TestData);
                        connection.Send(msg);
                        msg.Recycle();
                    }

                    // Receive 4 acks, Drop the middle two
                    capture.AssertPacketsToLocalCountEquals(4);
                    capture.ReorderPacketsForLocal(list => list.Sort(SortByPacketId.Instance));

                    var ack1 = capture.PeekPacketForLocal();
                    Assert.AreEqual(10, ack1[0]); // enum SendOptionInternal.Acknowledgement
                    Assert.AreEqual(0, ack1[1]);
                    Assert.AreEqual(1, ack1[2]);
                    Assert.AreEqual(255, ack1[3]);
                    capture.ReleasePacketsToLocal(1);

                    capture.DiscardPacketForLocal(2);

                    var ack4 = capture.PeekPacketForLocal();
                    Assert.AreEqual(10, ack4[0]); // enum SendOptionInternal.Acknowledgement
                    Assert.AreEqual(0, ack4[1]);
                    Assert.AreEqual(4, ack4[2]);
                    Assert.AreEqual(255, ack4[3]);
                    capture.ReleasePacketsToLocal(1);
                }

                // +1 for Hello packet, +4 for reliable despite the dropped ack
                Thread.Sleep(100); // The final ack has to actually be processed.
                Assert.AreEqual(3, connection.Statistics.AcknowledgementMessagesReceived);
                Assert.AreEqual(5, connection.Statistics.ReliablePacketsAcknowledged);
            }
        }

        private class SortByPacketId : IComparer<ByteSpan>
        {
            public static SortByPacketId Instance = new SortByPacketId();

            public int Compare(ByteSpan x, ByteSpan y)
            {
                ushort xId = BitConverter.ToUInt16(x.GetUnderlyingArray(), 1);
                ushort yId = BitConverter.ToUInt16(y.GetUnderlyingArray(), 1);
                return xId.CompareTo(yId);
            }
        }

        /// <summary>
        ///     Tests IPv4 connectivity.
        /// </summary>
        [TestMethod]
        public void UdpIPv4ConnectionTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                listener.Start();

                connection.Connect();

                Assert.AreEqual(ConnectionState.Connected, connection.State);

                Console.Write($"Client sent {connection.Statistics.TotalBytesSent} bytes ");
            }
        }

        /// <summary>
        ///     Tests IPv4 resilience to multiple hellos.
        /// </summary>
        [TestMethod]
        public void ConnectLikeAJerkTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                int connects = 0;
                listener.NewConnection += (obj) =>
                {
                    Interlocked.Increment(ref connects);
                };

                listener.Start();

                socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                var bytes = new byte[2];
                bytes[0] = (byte)UdpSendOption.Hello;
                for (int i = 0; i < 10; ++i)
                {
                    socket.SendTo(bytes, new IPEndPoint(IPAddress.Loopback, 4296));
                }

                Thread.Sleep(500);

                Assert.AreEqual(0, listener.ReceiveQueueLength);
                Assert.IsTrue(connects <= 1, $"Too many connections: {connects}");
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [TestMethod]
        public void MixedConnectionTest()
        {
            
            using (ThreadLimitedUdpConnectionListener listener2 = this.CreateListener(4, new IPEndPoint(IPAddress.IPv6Any, 4296), new ConsoleLogger(true), IPMode.IPv6))
            {
                listener2.Start();

                listener2.NewConnection += (evt) =>
                {
                    Console.WriteLine($"Connection: {evt.Connection.EndPoint}");
                };

                using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4296), new TestLogger()))
                {
                    connection.Connect();
                    Assert.AreEqual(ConnectionState.Connected, connection.State);
                }

                using (UdpConnection connection2 = this.CreateConnection(new IPEndPoint(IPAddress.IPv6Loopback, 4296), new TestLogger(), IPMode.IPv6))
                {
                    connection2.Connect();
                    Assert.AreEqual(ConnectionState.Connected, connection2.State);
                }
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [TestMethod]
        public void UdpIPv6ConnectionTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.IPv6Any, 4296), new TestLogger(), IPMode.IPv6))
            {
                listener.Start();

                using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4296), new TestLogger(), IPMode.IPv6))
                {
                    connection.Connect();
                }
            }
        }

        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpUnreliableServerToClientTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                TestHelper.RunServerToClientTest(listener, connection, 10, SendOption.None);
            }
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpReliableServerToClientTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                TestHelper.RunServerToClientTest(listener, connection, 10, SendOption.Reliable);
            }
        }
        
        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpUnreliableClientToServerTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                TestHelper.RunClientToServerTest(listener, connection, 10, SendOption.None);
            }
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpReliableClientToServerTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                TestHelper.RunClientToServerTest(listener, connection, 10, SendOption.Reliable);
            }
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [TestMethod]
        public virtual void KeepAliveClientTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                listener.Start();

                connection.Connect();
                connection.KeepAliveInterval = 100;

                Thread.Sleep(1050);    //Enough time for ~10 keep alive packets

                Assert.AreEqual(ConnectionState.Connected, connection.State);
                Assert.IsTrue(
                    connection.Statistics.TotalBytesSent >= 30 &&
                    connection.Statistics.TotalBytesSent <= 50,
                    "Sent: " + connection.Statistics.TotalBytesSent
                );
            }
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [TestMethod]
        public void KeepAliveServerTest()
        {
            ManualResetEvent mutex = new ManualResetEvent(false);

            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                UdpConnection client = null;
                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    client = (UdpConnection)args.Connection;
                    client.KeepAliveInterval = 100;

                    Thread timeoutThread = new Thread(() =>
                    {
                        Thread.Sleep(1050);    //Enough time for ~10 keep alive packets
                        mutex.Set();
                    });
                    timeoutThread.Start();
                };

                listener.Start();

                connection.Connect();

                mutex.WaitOne();

                Assert.AreEqual(ConnectionState.Connected, client.State);

                Assert.IsTrue(
                    client.Statistics.TotalBytesSent >= 27 &&
                    client.Statistics.TotalBytesSent <= 50,
                    "Sent: " + client.Statistics.TotalBytesSent
                );
            }
        }

        /// <summary>
        ///     Tests disconnection from the client.
        /// </summary>
        [TestMethod]
        public void ClientDisconnectTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                ManualResetEvent mutex = new ManualResetEvent(false);
                ManualResetEvent mutex2 = new ManualResetEvent(false);

                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    args.Connection.Disconnected += delegate (object sender2, DisconnectedEventArgs args2)
                    {
                        mutex2.Set();
                    };

                    mutex.Set();
                };

                listener.Start();

                connection.Connect();

                mutex.WaitOne(1000);
                Assert.AreEqual(ConnectionState.Connected, connection.State);

                connection.Disconnect("Testing");

                mutex2.WaitOne(1000);
                Assert.AreEqual(ConnectionState.NotConnected, connection.State);
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [TestMethod]
        public void ServerDisconnectTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                SemaphoreSlim mutex = new SemaphoreSlim(0, 100);
                ManualResetEventSlim serverMutex = new ManualResetEventSlim(false);

                connection.Disconnected += delegate (object sender, DisconnectedEventArgs args)
                {
                    mutex.Release();
                };

                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    mutex.Release();

                    // This has to be on a new thread because the client will go straight from Connecting to NotConnected
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        serverMutex.Wait(500);
                        args.Connection.Disconnect("Testing");
                    });
                };

                listener.Start();

                connection.Connect();

                mutex.Wait(500);
                Assert.AreEqual(ConnectionState.Connected, connection.State);

                serverMutex.Set();

                mutex.Wait(500);
                Assert.AreEqual(ConnectionState.NotConnected, connection.State);
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [TestMethod]
        public void ServerExtraDataDisconnectTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                string received = null;
                ManualResetEvent mutex = new ManualResetEvent(false);

                connection.Disconnected += delegate (object sender, DisconnectedEventArgs args)
                {
                    // We don't own the message, we have to read the string now
                    received = args.Message.ReadString();
                    mutex.Set();
                };

                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    MessageWriter writer = MessageWriter.Get(SendOption.None);
                    writer.Write("Goodbye");
                    args.Connection.Disconnect("Testing", writer);
                };

                listener.Start();

                connection.Connect();

                mutex.WaitOne(5000);

                Assert.IsNotNull(received);
                Assert.AreEqual("Goodbye", received);
            }
        }
    }
}
