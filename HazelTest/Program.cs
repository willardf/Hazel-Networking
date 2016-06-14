using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Hazel.Udp;

namespace Hazel
{
    class Program
    {
        static void Main2(string[] args)
        {
            /*using (Connection connection = new UdpClientConnection(new NetworkEndPoint(IPAddress.Loopback, 4296)))
            {
                connection.Connect();
                connection.DataReceived += connection_DataReceived;
                connection.Disconnected += connection_Disconnected;

                while (Console.ReadLine() != "Quit")
                    connection.SendBytes(new byte[] { 0, 1, 2, 3, 4 }, SendOption.Reliable);
            }*/

            /*using (ConnectionListener listener = new UdpConnectionListener(IPAddress.Any, 4296, IPMode.IPv4))
            {
                listener.NewConnection += delegate(object sender, NewConnectionEventArgs a)
                {
                    Console.WriteLine("Hi! " + a.Connection.EndPoint.ToString());

                    a.Connection.DataReceived += delegate(object sender2, DataEventArgs a2)
                    {
                        Console.WriteLine("Received " + a2.Bytes.Length + " of data!");
                        a.Connection.SendBytes(a2.Bytes, a2.SendOption);
                    };

                    a.Connection.Disconnected += connection_Disconnected;
                };

                listener.Start();

                Console.ReadKey();
            }*/
        }

        static void connection_DataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Bytes.Length);
        }

        static void connection_Disconnected(object sender, DisconnectedEventArgs e)
        {
            Console.WriteLine("Bye Bye!");
        }
    }
}
