using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading;

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
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection())
            {
                listener.Start();

                NetworkEndPoint ep = new NetworkEndPoint(IPAddress.Loopback, 4296);
                connection.Connect(ep);

                //Connection fields
                Assert.AreEqual(ep, connection.EndPoint);

                //UdpConnection fields
                Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 4296), connection.RemoteEndPoint);
                Assert.AreEqual(0, connection.Statistics.DataBytesSent);
                Assert.AreEqual(0, connection.Statistics.DataBytesReceived);
            }
        }

        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpUnreliableServerToClientTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection())
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
            using (UdpConnection connection = new UdpClientConnection())
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
            using (UdpConnection connection = new UdpClientConnection())
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
            using (UdpConnection connection = new UdpClientConnection())
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
            using (UdpConnection connection = new UdpClientConnection())
            {
                listener.Start();

                connection.Connect(new NetworkEndPoint(IPAddress.Loopback, 4296));
                connection.KeepAliveInterval = 100;

                System.Threading.Thread.Sleep(1100);    //Enough time for ~10 keep alive packets

                Assert.IsTrue(
                    connection.Statistics.TotalBytesSent >= 27
                        && connection.Statistics.TotalBytesSent <= 33,
                    "Received: " + connection.Statistics.TotalBytesSent
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
            using (UdpConnection connection = new UdpClientConnection())
            {
                listener.NewConnection += delegate(object sender, NewConnectionEventArgs args)
                {
                    ((UdpConnection)args.Connection).KeepAliveInterval = 100;

                    Thread.Sleep(1100);    //Enough time for ~10 keep alive packets

                    Assert.IsTrue(
                        args.Connection.Statistics.TotalBytesSent >= 27
                            && args.Connection.Statistics.TotalBytesSent <= 33,
                        "Received: " + args.Connection.Statistics.TotalBytesSent
                    );

                    mutex.Set();
                };

                listener.Start();

                connection.Connect(new NetworkEndPoint(IPAddress.Loopback, 4296));

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
            using (UdpConnection connection = new UdpClientConnection())
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
            using (UdpConnection connection = new UdpClientConnection())
            {
                TestHelper.RunServerDisconnectTest(listener, connection);
            }
        }
    }
}
