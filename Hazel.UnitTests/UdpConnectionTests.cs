using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading;

using Hazel.Udp;

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

            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection(ep))
            {
                listener.Start();

                connection.Connect();

                //Connection fields
                Assert.AreEqual(ep, connection.EndPoint);

                //UdpConnection fields
                Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 4296), connection.RemoteEndPoint);
                Assert.AreEqual(0, connection.Statistics.DataBytesSent);
                Assert.AreEqual(0, connection.Statistics.DataBytesReceived);
            }
        }

        /// <summary>
        ///     Tests IPv4 connectivity.
        /// </summary>
        [TestMethod]
        public void UdpIPv4ConnectionTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296, IPMode.IPv4))
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
        public void UdpDualModeConnectionTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296, IPMode.IPv4AndIPv6))
            {
                listener.Start();

                using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296, IPMode.IPv4)))
                {
                    connection.Connect();
                }

                using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296, IPMode.IPv4AndIPv6)))
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
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunServerToClientTest(listener, connection, 1, 3, SendOption.None);
            }
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpReliableServerToClientTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunServerToClientTest(listener, connection, 3, 3, SendOption.Reliable);
            }
        }

        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpUnreliableClientToServerTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunClientToServerTest(listener, connection, 1, 3, SendOption.None);
            }
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpReliableClientToServerTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunClientToServerTest(listener, connection, 3, 3, SendOption.Reliable);
            }
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [TestMethod]
        public void KeepAliveClientTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();

                connection.Connect();
                connection.KeepAliveInterval = 100;

                System.Threading.Thread.Sleep(1100);    //Enough time for ~10 keep alive packets

                Assert.IsTrue(
                    connection.Statistics.TotalBytesSent >= 27 &&
                    connection.Statistics.TotalBytesSent <= 33,
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

            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.NewConnection += delegate(object sender, NewConnectionEventArgs args)
                {
                    ((UdpConnection)args.Connection).KeepAliveInterval = 100;

                    Thread.Sleep(1100);    //Enough time for ~10 keep alive packets

                    Assert.IsTrue(
                        args.Connection.Statistics.TotalBytesSent >= 27 &&
                        args.Connection.Statistics.TotalBytesSent <= 33,
                        "Sent: " + connection.Statistics.TotalBytesSent
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
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
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
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunServerDisconnectTest(listener, connection);
            }
        }
    }
}
