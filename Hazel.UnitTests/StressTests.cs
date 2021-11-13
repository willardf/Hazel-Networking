using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Hazel.Dtls;
using Hazel.Udp;
using Hazel.Udp.FewerThreads;
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

        // This was a thing that happened to us a DDoS. Mildly instructional that we straight up ignore it.
        public void SourceAmpAttack()
        {
            var localEp = new IPEndPoint(IPAddress.Any, 11710);
            var serverEp = new IPEndPoint(IPAddress.Loopback, 11710);
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(4, localEp, new ConsoleLogger(true)))
            {
                listener.Start();

                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.DontFragment = false;

                try
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    socket.IOControl(SIO_UDP_CONNRESET, new byte[1], null);
                }
                catch { } // Only necessary on Windows

                string byteAsHex = "f23c 92d1 c277 001b 54c2 50c1 0800 4500 0035 7488 0000 3b11 2637 062f ac75 2d4f 0506 a7ea 5607 0021 5e07 ffff ffff 5453 6f75 7263 6520 456e 6769 6e65 2051 7565 7279 00";
                byte[] bytes = StringToByteArray(byteAsHex.Replace(" ", ""));
                socket.SendTo(bytes, serverEp);

                while (socket.Poll(50000, SelectMode.SelectRead))
                {
                    byte[] buffer = new byte[1024];
                    int len = socket.Receive(buffer);
                    Console.WriteLine($"got {len} bytes: " + string.Join(" ", buffer.Select(b => b.ToString("X"))));
                    Console.WriteLine($"got {len} bytes: " + string.Join(" ", buffer.Select(b => (char)b)));
                }
            }
        }
        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length / 2)
                             .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
                             .ToArray();
        }
    }
}
