using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hazel;
using Hazel.Udp;

namespace HazelExample
{
    class ClientExample
    {
        static Connection connection;

        public static void Main(string[] args)
        {
            NetworkEndPoint endPoint = new NetworkEndPoint("13.74.185.0", 4296);

            connection = new UdpClientConnection(endPoint);

            connection.DataReceived += DataReceived;

            Console.WriteLine("Connecting!");

            connection.Connect();

            connection.SendBytes(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            Console.WriteLine("Press any key to continue...");

            Console.ReadKey();

            connection.Close();
        }

        private static void DataReceived(object sender, DataReceivedEventArgs args)
        {
            Console.WriteLine("Received (" + string.Join<byte>(", ", args.Bytes) + ") from " + connection.EndPoint.ToString());

            args.Recycle();
        }
    }
}
