using Hazel.Udp;
using Hazel.Udp.FewerThreads;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;

namespace Hazel.UnitTests.Reliability
{
    [TestClass]
    public class PacketDropTests
    {
        // This test fails because even at 10% packet drop and 10ms 
        [TestMethod]
        public void SustainedPacketLossShouldBeFine()
        {
            var serverEp = new IPEndPoint(IPAddress.Loopback, 23432);
            var clientEp = new IPEndPoint(IPAddress.Loopback, 23433);

            var logger = new ConsoleLogger(true);

            using (SocketCapture capture = new UnreliableSocketCapture(clientEp, serverEp, logger))
            using (ThreadLimitedUdpConnectionListener server = new ThreadLimitedUdpConnectionListener(4, serverEp, logger))
            using (UnityUdpClientConnection client = new UnityUdpClientConnection(logger, clientEp))
            using (Timer timer = new Timer(_ =>
                {
                    var up = Stopwatch.StartNew();
                    var cnt = client.FixedUpdate();
                    if (cnt != 0)
                    {
                        logger.WriteInfo($"Took {up.ElapsedMilliseconds}ms to resend {cnt} pkts");
                    }
                }, null, 100, 100))
            { 
                server.ReliableResendPollRateMs = 10;
                UdpConnection serverClient = null;
                server.NewConnection += (evt) => serverClient = (UdpConnection)evt.Connection;

                server.Start();
                client.Connect();                

                var msg = MessageWriter.Get(SendOption.Reliable);
                msg.Length = 500;
                for (int i = 0; i < 100; ++i)
                {
                    client.Send(msg);
                    // client.FixedUpdate();
                    Thread.Sleep(1000 / 30);
                }

                while (serverClient.Statistics.ReliableMessagesReceived < 101)
                {
                    Assert.AreEqual(ConnectionState.Connected, client.State);
                    // client.FixedUpdate();
                    Thread.Sleep(1000 / 30);
                }

                Thread.Sleep(2000);

                Assert.AreEqual(serverClient.Statistics.ReliableMessagesReceived, client.Statistics.ReliableMessagesSent);
                Assert.IsTrue(6 < client.Statistics.MessagesResent);
                Assert.IsTrue(10 > client.AveragePingMs, "Ping was kinda high: " + client.AveragePingMs);

                msg.Recycle();
            }
        }

        private class UnreliableSocketCapture : SocketCapture
        {            
            private Random r = new Random(10);

            public UnreliableSocketCapture(IPEndPoint captureEndpoint, IPEndPoint remoteEndPoint, ILogger logger = null) 
                : base(captureEndpoint, remoteEndPoint, logger)
            {
            }

            protected override bool ShouldSendToRemote()
            {
                // 10% drop rate
                return r.NextDouble() > .1f;
            }
        }
    }
}
