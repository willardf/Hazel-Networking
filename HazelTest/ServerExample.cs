using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using Hazel;
using Hazel.Tcp;
/*
namespace HazelExample
{
    class ServerExample
    {
        static ConnectionListener listener;

        public static void Main(string[] args)
        {
            listener = new TcpConnectionListener(IPAddress.Any, 4296);

            listener.NewConnection += NewConnectionHandler;

            Console.WriteLine("Starting server!");

            listener.Start();

            Console.WriteLine("Press any key to continue...");

            Console.ReadKey();

            listener.Close();
        }

        static void NewConnectionHandler(object sender, NewConnectionEventArgs args)
        {
            Console.WriteLine("New connection from " + args.Connection.EndPoint.ToString());

            args.Connection.DataReceived += DataReceivedHandler;

            args.Recycle();
        }

        private static void DataReceivedHandler(object sender, DataEventArgs args)
        {
            Connection connection = (Connection)sender;

            Console.WriteLine("Received (" + string.Join<byte>(", ", args.Bytes) + ") from " + connection.EndPoint.ToString());

            connection.SendBytes(args.Bytes, args.SendOption);

            args.Recycle();
        }
    }
}
*/