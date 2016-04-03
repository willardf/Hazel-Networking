using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;

namespace Hazel.UnitTests
{
    [TestClass]
    public class UdpConnectionTests
    {
        /// <summary>
        ///     Tests the fields on UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpConnectionFieldTest()
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
                Assert.AreEqual(1, connection.Statistics.DataBytesSent);
                Assert.AreEqual(0, connection.Statistics.DataBytesReceived);
            }
        }

        /// <summary>
        ///     Tests sending and receiving on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void UdpConnectionSendReceiveTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296))
            using (UdpConnection connection = new UdpClientConnection())
            {
                TestHelper.RunSendReceiveTest(listener, connection, 1, 1, 2);
            }
        }
    }
}
