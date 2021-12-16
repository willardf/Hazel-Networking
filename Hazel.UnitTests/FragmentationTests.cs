using System;
using System.Linq;
using System.Net;
using System.Threading;
using Hazel.Udp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class FragmentationTests
    {
        private readonly byte[] _testData = Enumerable.Range(0, 10000).Select(x => (byte)x).ToArray();

        [TestMethod]
        public void ReliableSendTest()
        {
            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                var manualResetEvent = new ManualResetEventSlim(false);
                DataReceivedEventArgs? data = null;

                listener.NewConnection += e =>
                {
                    e.Connection.DataReceived += de =>
                    {
                        data = de;
                        manualResetEvent.Set();
                    };
                };

                listener.Start();
                connection.Connect();

                connection.SendBytes(_testData, SendOption.Reliable);

                manualResetEvent.Wait(TimeSpan.FromSeconds(5));

                Assert.IsNotNull(data);

                Assert.AreEqual(SendOption.Reliable, data.Value.SendOption);

                var messageReader = data.Value.Message;
                var received = new byte[messageReader.Buffer.Length - messageReader.Offset - messageReader.Position];
                Array.Copy(messageReader.Buffer, messageReader.Offset + messageReader.Position, received, 0, received.Length);

                CollectionAssert.AreEqual(_testData, received);
            }
        }

        [TestMethod]
        public void UnreliableSendTest()
        {
            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();
                connection.Connect();

                Assert.AreEqual("Unreliable messages can't be bigger than MTU", Assert.ThrowsException<HazelException>(() =>
                {
                    connection.SendBytes(_testData);
                }).Message);
            }
        }
    }
}
