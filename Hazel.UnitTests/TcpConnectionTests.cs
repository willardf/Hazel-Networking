using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading;

using Hazel.Tcp;

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
            NetworkEndPoint ep = new NetworkEndPoint(IPAddress.Loopback, 4296);

            using (TcpConnectionListener listener = new TcpConnectionListener(IPAddress.Any, 4296))
            using (TcpConnection connection = new TcpConnection(ep))
            {
                listener.Start();

                connection.Connect();

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
            using (TcpConnection connection = new TcpConnection(new NetworkEndPoint(IPAddress.Loopback, 4296, IPMode.IPv4)))
            {
                listener.Start();

                connection.Connect();
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

                using (TcpConnection connection = new TcpConnection(new NetworkEndPoint(IPAddress.Loopback, 4296, IPMode.IPv4)))
                {
                    connection.Connect();
                }

                using (TcpConnection connection = new TcpConnection(new NetworkEndPoint(IPAddress.Loopback, 4296, IPMode.IPv4AndIPv6)))
                {
                    connection.Connect();
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
            using (TcpConnection connection = new TcpConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
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
            using (TcpConnection connection = new TcpConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
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
            using (TcpConnection connection = new TcpConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
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
            using (TcpConnection connection = new TcpConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                TestHelper.RunServerDisconnectTest(listener, connection);
            }
        }
    }
}
