using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Hazel;
using System.Net;
using System.Threading;
using System.Diagnostics;

namespace Hazel.UnitTests
{
    [TestClass]
    public static class TestHelper
    {
        /// <summary>
        ///     Runs a general test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunSendReceiveTest(ConnectionListener listener, Connection connection, int headerSize, int handshakeSize, int totalHandshakeSize)
        {
            //Setup meta stuff 
            byte[] data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            AutoResetEvent mutex = new AutoResetEvent(false);

            //Setup listener
            listener.NewConnection += delegate(object sender, NewConnectionEventArgs args)
            {
                args.Connection.WriteBytes(data);
                Assert.AreEqual(data.Length, args.Connection.Statistics.DataBytesSent);
                Assert.AreEqual(0, args.Connection.Statistics.DataBytesReceived);
                Assert.AreEqual(data.Length + headerSize, args.Connection.Statistics.TotalBytesSent);
                Assert.AreEqual(0, args.Connection.Statistics.TotalBytesReceived);
            };

            listener.Start();

            //Setup conneciton
            connection.DataReceived += delegate(object sender, DataEventArgs args)
            {
                Trace.WriteLine("Data was received correctly.");

                for (int i = 0; i < data.Length; i++)
                {
                    Assert.AreEqual(data[i], args.Bytes[i]);
                }

                mutex.Set();
            };

            connection.Connect(new NetworkEndPoint(IPAddress.Loopback, 4296));

            //Wait until data is received
            mutex.WaitOne();

            Assert.AreEqual(handshakeSize, connection.Statistics.DataBytesSent);
            Assert.AreEqual(data.Length, connection.Statistics.DataBytesReceived);
            Assert.AreEqual(totalHandshakeSize, connection.Statistics.TotalBytesSent);
            Assert.AreEqual(data.Length + headerSize, connection.Statistics.TotalBytesReceived);
        }
    }
}
