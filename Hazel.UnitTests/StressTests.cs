using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hazel.Udp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class StressTests
    {
        // [TestMethod]
        public void StressTestOpeningConnections()
        {
            // Start a listener in another process, or even better, 
            // adjust the target IP and start listening on another computer.
            var ep = new IPEndPoint(IPAddress.Loopback, 22023);
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
