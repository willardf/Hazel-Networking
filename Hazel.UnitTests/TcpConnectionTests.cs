using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;

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
        ///     Tests sending and receiving on the TcpConnection.
        /// </summary>
        [TestMethod]
        public void TcpServerToClientTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(IPAddress.Any, 4296))
            using (TcpConnection connection = new TcpConnection())
            {
                TestHelper.RunServerToClientTest(listener, connection, 4, 0, 0, SendOption.OrderedFragmentedReliable);
            }
        }
    }
}
