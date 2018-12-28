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
        [TestMethod]
        public void StressTest()
        {
            var ep = new NetworkEndPoint(IPAddress.Loopback, 22023);
            Parallel.For(0, 10000,
                new ParallelOptions { MaxDegreeOfParallelism = 64 },
                (i) => {
                    
                var connection = new UdpClientConnection(ep);
                connection.KeepAliveInterval = 50;

                connection.Connect(new byte[5]);
            });
        }
    }
}
