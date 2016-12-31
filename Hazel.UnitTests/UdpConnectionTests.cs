using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading;

using Hazel.Udp;
using System.Linq;

namespace Hazel.UnitTests
{
    [TestClass]
    public class UdpConnectionTests
    {
        /// <summary>
        ///     Tests the fields on UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpFieldTest()
        {
            NetworkEndPoint ep = new NetworkEndPoint(IPAddress.Loopback, 4296);

            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
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
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296, IPMode.IPv4)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296, IPMode.IPv4)))
            {
                listener.Start();

                listener.NewConnection += delegate (object sender, NewConnectionEventArgs e)
                {
                    Assert.IsTrue(Enumerable.SequenceEqual(e.HandshakeData, new byte[] { 1, 2, 3, 4, 5, 6 }));
                };

                connection.Connect(new byte[] { 1, 2, 3, 4, 5, 6 });
            }
        }

        /// <summary>
        ///     Tests IPv4 connectivity.
        /// </summary>
        [TestMethod]
        public void UdpIPv4ConnectionTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296, IPMode.IPv4)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296, IPMode.IPv4)))
            {
                listener.Start();

                connection.Connect();
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [TestMethod]
        public void UdpIPv6ConnectionTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.IPv6Any, 4296, IPMode.IPv6)))
            {
                listener.Start();

                using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.IPv6Loopback, 4296, IPMode.IPv6)))
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
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
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
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunServerToClientTest(listener, connection, 10, SendOption.Reliable);
            }
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpFragmentedServerToClientTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunServerToClientTest(listener, connection, (int)(connection.FragmentSize * 9.5), SendOption.FragmentedReliable);
            }
        }

        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpUnreliableClientToServerTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
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
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunClientToServerTest(listener, connection, 10, SendOption.Reliable);
            }
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpFragmentedClientToServerTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunClientToServerTest(listener, connection, (int)(connection.FragmentSize * 9.5), SendOption.FragmentedReliable);
            }
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [TestMethod]
        public void KeepAliveClientTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();

                connection.Connect();
                connection.KeepAliveInterval = 100;

                System.Threading.Thread.Sleep(1050);    //Enough time for ~10 keep alive packets

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

            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.NewConnection += delegate(object sender, NewConnectionEventArgs args)
                {
                    ((UdpConnection)args.Connection).KeepAliveInterval = 100;

                    Thread.Sleep(1050);    //Enough time for ~10 keep alive packets

                    Assert.IsTrue(
                        args.Connection.Statistics.TotalBytesSent >= 30 &&
                        args.Connection.Statistics.TotalBytesSent <= 50,
                        "Sent: " + args.Connection.Statistics.TotalBytesSent
                    );

                    mutex.Set();
                };

                listener.Start();

                connection.Connect();

                mutex.WaitOne();
            }
        }

        /// <summary>
        ///     Tests disconnection from the client.
        /// </summary>
        [TestMethod]
        public void ClientDisconnectTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunClientDisconnectTest(listener, connection);
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [TestMethod]
        public void ServerDisconnectTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunServerDisconnectTest(listener, connection);
            }
        }
    }
}
