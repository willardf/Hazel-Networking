using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hazel.Udp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class UnitTest1
    {
        // [TestMethod]
        public void StressTest()
        {
            Parallel.For(0, 10000,
                new ParallelOptions { MaxDegreeOfParallelism = 16 },
                (i) =>
            {
                var ep = new NetworkEndPoint(IPAddress.Loopback, 22023);
                using (var connection = new UdpClientConnection(ep))
                {
                    connection.Connect();
                    Thread.Sleep(100);
                }
            });
        }
    }
}
