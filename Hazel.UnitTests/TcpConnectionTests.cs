using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading;

namespace Hazel.UnitTests
{
    [TestClass]
    public class TcpConnectionTests
    {
        /// <summary>
        ///     Tests the fields on TcpConnection.
        /// </summary>
        [TestMethod]
        public void TcpFieldTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(IPAddress.Any, 4296))
            using (TcpConnection connection = new TcpConnection())
            {
                listener.Start();

                NetworkEndPoint ep = new NetworkEndPoint(IPAddress.Loopback, 4296);
                connection.Connect(ep);

                //Connection fields
                Assert.AreEqual(ep, connection.EndPoint);

                //TcpConnection fields
                Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 4296), connection.RemoteEndPoint);
                Assert.AreEqual(0, connection.Statistics.DataBytesSent);
                Assert.AreEqual(0, connection.Statistics.DataBytesReceived);
            }
        }

        /// <summary>
        ///     Tests IPv4 connectivity.
        /// </summary>
        [TestMethod]
        public void TcpIPv4ConnectionTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(IPAddress.Any, 4296, IPMode.IPv4))
            using (TcpConnection connection = new TcpConnection())
            {
                listener.Start();

                connection.Connect(new NetworkEndPoint(IPAddress.Loopback, 4296, IPMode.IPv4));
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [TestMethod]
        public void TcpDualModeConnectionTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(IPAddress.Any, 4296, IPMode.IPv4AndIPv6))
            {
                listener.Start();

                using (TcpConnection connection = new TcpConnection())
                {
                    connection.Connect(new NetworkEndPoint(IPAddress.Loopback, 4296, IPMode.IPv4));
                }

                using (TcpConnection connection = new TcpConnection())
                {
                    connection.Connect(new NetworkEndPoint(IPAddress.Loopback, 4296, IPMode.IPv4AndIPv6));
                }
            }
        }

        /// <summary>
        ///     Tests sending and receiving on the TcpConnection.
        /// </summary>
        [TestMethod]
        public void TcpServerToClientTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(IPAddress.Any, 4296))
            using (TcpConnection connection = new TcpConnection())
            {
                TestHelper.RunServerToClientTest(listener, connection, 4, 0, SendOption.FragmentedReliable);
            }
        }

        /// <summary>
        ///     Tests sending and receiving on the TcpConnection.
        /// </summary>
        [TestMethod]
        public void TcpClientToServerTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(IPAddress.Any, 4296))
            using (TcpConnection connection = new TcpConnection())
            {
                TestHelper.RunClientToServerTest(listener, connection, 4, 0, SendOption.FragmentedReliable);
            }
        }

        /// <summary>
        ///     Tests disconnection from the client.
        /// </summary>
        [TestMethod]
        public void ClientDisconnectTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(IPAddress.Any, 4296))
            using (TcpConnection connection = new TcpConnection())
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
            using (TcpConnectionListener listener = new TcpConnectionListener(IPAddress.Any, 4296))
            using (TcpConnection connection = new TcpConnection())
            {
                TestHelper.RunServerDisconnectTest(listener, connection);
            }
        }
    }
}
