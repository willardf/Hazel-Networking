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
        internal static void RunServerToClientTest(ConnectionListener listener, Connection connection, int headerSize, int totalHandshakeSize, SendOption sendOption)
        {
            //Setup meta stuff 
            byte[] data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            ManualResetEvent mutex = new ManualResetEvent(false);

            //Setup listener
            listener.NewConnection += delegate(object sender, NewConnectionEventArgs args)
            {
                Assert.AreEqual(0, args.Connection.Statistics.DataBytesReceived);
                Assert.AreEqual(0, args.Connection.Statistics.TotalBytesReceived);
                
                args.Connection.SendBytes(data, sendOption);
                
                Assert.AreEqual(data.Length, args.Connection.Statistics.DataBytesSent);
                Assert.AreEqual(data.Length + headerSize, args.Connection.Statistics.TotalBytesSent);
            };

            listener.Start();

            //Setup conneciton
            connection.DataReceived += delegate(object sender, DataReceivedEventArgs args)
            {
                Trace.WriteLine("Data was received correctly.");

                for (int i = 0; i < data.Length; i++)
                {
                    Assert.AreEqual(data[i], args.Bytes[i]);
                }

                Assert.AreEqual(sendOption, args.SendOption);
                
                mutex.Set();
            };

            connection.Connect();

            //Wait until data is received
            mutex.WaitOne();

            Assert.AreEqual(0, connection.Statistics.DataBytesSent);
            Assert.AreEqual(data.Length, connection.Statistics.DataBytesReceived);
            Assert.AreEqual(totalHandshakeSize, connection.Statistics.TotalBytesSent);
            Assert.AreEqual(data.Length + headerSize, connection.Statistics.TotalBytesReceived);
        }

        /// <summary>
        ///     Runs a general test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunClientToServerTest(ConnectionListener listener, Connection connection, int headerSize, int totalHandshakeSize, SendOption sendOption)
        {
            //Setup meta stuff 
            byte[] data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            ManualResetEvent mutex = new ManualResetEvent(false);
            ManualResetEvent mutex2 = new ManualResetEvent(false);

            //Setup listener
            listener.NewConnection += delegate(object sender, NewConnectionEventArgs args)
            {
                args.Connection.DataReceived += delegate(object innerSender, DataReceivedEventArgs innerArgs)
                {
                    Trace.WriteLine("Data was received correctly.");

                    for (int i = 0; i < data.Length; i++)
                    {
                        Assert.AreEqual(data[i], innerArgs.Bytes[i]);
                    }

                    Assert.AreEqual(sendOption, innerArgs.SendOption);

                    Assert.AreEqual(0, args.Connection.Statistics.DataBytesSent);
                    Assert.AreEqual(data.Length, args.Connection.Statistics.DataBytesReceived);
                    Assert.AreEqual(0, args.Connection.Statistics.TotalBytesSent);
                    Assert.AreEqual(data.Length + headerSize, args.Connection.Statistics.TotalBytesReceived);

                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            //Connect
            connection.Connect();

            mutex.WaitOne();

            connection.SendBytes(data, sendOption);

            //Wait until data is received
            mutex2.WaitOne();

            Assert.AreEqual(data.Length, connection.Statistics.DataBytesSent);
            Assert.AreEqual(0, connection.Statistics.DataBytesReceived);
            Assert.AreEqual(totalHandshakeSize + data.Length + headerSize, connection.Statistics.TotalBytesSent);
            Assert.AreEqual(0, connection.Statistics.TotalBytesReceived);
        }

        /// <summary>
        ///     Runs a server disconnect test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunServerDisconnectTest(ConnectionListener listener, Connection connection)
        {
            ManualResetEvent mutex = new ManualResetEvent(false);

            connection.Disconnected += delegate(object sender, DisconnectedEventArgs args)
            {
                mutex.Set();
            };

            listener.NewConnection += delegate(object sender, NewConnectionEventArgs args)
            {
                args.Connection.Close();
            };

            listener.Start();

            connection.Connect();

            mutex.WaitOne();
        }

        /// <summary>
        ///     Runs a client disconnect test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunClientDisconnectTest(ConnectionListener listener, Connection connection)
        {
            ManualResetEvent mutex = new ManualResetEvent(false);
            ManualResetEvent mutex2 = new ManualResetEvent(false);

            listener.NewConnection += delegate(object sender, NewConnectionEventArgs args)
            {
                args.Connection.Disconnected += delegate(object sender2, DisconnectedEventArgs args2)
                {
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            connection.Connect();

            mutex.WaitOne();

            connection.Close();

            mutex2.WaitOne();
        }
    }
}
