using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading;
using Hazel.Udp;
using Hazel.Udp.FewerThreads;
using System.Net.Sockets;

namespace Hazel.UnitTests
{
    [TestClass]
    public class ThreadLimitedUdpConnectionTests
    {
        [TestMethod]
        public void ServerDisposeDisconnectsTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(ep))
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
        public void ClientServerDisposeDisconnectsTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(ep))
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

            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(ep))
            {
                listener.Start();

                connection.Connect();

                //Connection fields
                Assert.AreEqual(ep, connection.EndPoint);

                //UdpConnection fields
                Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 4296), connection.RemoteEndPoint);
                Assert.AreEqual(1, connection.Statistics.DataBytesSent);
                Assert.AreEqual(0, connection.Statistics.DataBytesReceived);
            }
        }

        [TestMethod]
        public void UdpHandshakeTest()
        {
            byte[] TestData = new byte[] { 1, 2, 3, 4, 5, 6 };
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();

                MessageReader output = null;
                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    output = e.HandshakeData;
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
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                MessageReader output = null;
                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    e.Connection.DataReceived += delegate (DataReceivedEventArgs evt)
                    {
                        output = evt.Message;
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

        /// <summary>
        ///     Tests IPv4 connectivity.
        /// </summary>
        [TestMethod]
        public void UdpIPv4ConnectionTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();

                connection.Connect();
            }
        }

        /// <summary>
        ///     Tests IPv4 resilience to multiple hellos.
        /// </summary>
        [TestMethod]
        public void ConnectLikeAJerkTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                int connects = 0;
                listener.NewConnection += (obj) =>
                {
                    Interlocked.Increment(ref connects);
                    obj.HandshakeData.Recycle();
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
                Assert.AreEqual(1, connects);
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [TestMethod]
        public void MixedConnectionTest()
        {
            
            using (ThreadLimitedUdpConnectionListener listener2 = new ThreadLimitedUdpConnectionListener(4, new IPEndPoint(IPAddress.IPv6Any, 4296), new ConsoleLogger(), IPMode.IPv6))
            {
                listener2.Start();

                listener2.NewConnection += (evt) =>
                {
                    Console.WriteLine($"Connection: {evt.Connection.EndPoint}");
                };

                using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4296)))
                {
                    connection.Connect();
                    Assert.AreEqual(ConnectionState.Connected, connection.State);
                }

                using (UdpConnection connection2 = new UdpClientConnection(new IPEndPoint(IPAddress.IPv6Loopback, 4296), IPMode.IPv6))
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
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger(), IPMode.IPv6))
            {
                listener.Start();

                using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4296), IPMode.IPv6))
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
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
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
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
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
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
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
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunClientToServerTest(listener, connection, 10, SendOption.Reliable);
            }
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [TestMethod]
        public void KeepAliveClientTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
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

            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                UdpConnection client = null;
                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    client = (UdpConnection)args.Connection;
                    client.KeepAliveInterval = 100;

                    Thread.Sleep(1050);    //Enough time for ~10 keep alive packets

                    mutex.Set();
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
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
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

                mutex.WaitOne();

                connection.Disconnect("Testing");

                mutex2.WaitOne();
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [TestMethod]
        public void ServerDisconnectTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {

                ManualResetEvent mutex = new ManualResetEvent(false);

                connection.Disconnected += delegate (object sender, DisconnectedEventArgs args)
                {
                    mutex.Set();
                };

                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    args.Connection.Disconnect("Testing");
                };

                listener.Start();

                connection.Connect();

                mutex.WaitOne();
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [TestMethod]
        public void ServerExtraDataDisconnectTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(2, new IPEndPoint(IPAddress.Any, 4296), new NullLogger()))
            using (UdpConnection connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                MessageReader received = null;
                ManualResetEvent mutex = new ManualResetEvent(false);

                connection.Disconnected += delegate (object sender, DisconnectedEventArgs args)
                {
                    received = args.Message;
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

                mutex.WaitOne();

                Assert.IsNotNull(received);
                Assert.AreEqual("Goodbye", received.ReadString());
            }
        }
    }
}
