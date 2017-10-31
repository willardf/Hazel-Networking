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
        internal static void RunServerToClientTest(ConnectionListener listener, Connection connection, int dataSize, SendOption sendOption)
        {
            //Setup meta stuff 
            byte[] data = BuildData(dataSize);
            ManualResetEvent mutex = new ManualResetEvent(false);

            //Setup listener
            listener.NewConnection += delegate(object sender, NewConnectionEventArgs args)
            {
                args.Connection.SendBytes(data, sendOption);
            };

            listener.Start();

            //Setup conneciton
            connection.DataReceived += delegate(object sender, DataReceivedEventArgs args)
            {
                Trace.WriteLine("Data was received correctly.");

                Assert.AreEqual(data.Length, args.Bytes.Length);

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
        }

        /// <summary>
        ///     Runs a general test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunClientToServerTest(ConnectionListener listener, Connection connection, int dataSize, SendOption sendOption)
        {
            //Setup meta stuff 
            byte[] data = BuildData(dataSize);
            ManualResetEvent mutex = new ManualResetEvent(false);
            ManualResetEvent mutex2 = new ManualResetEvent(false);

            //Setup listener
            listener.NewConnection += delegate(object sender, NewConnectionEventArgs args)
            {
                args.Connection.DataReceived += delegate(object innerSender, DataReceivedEventArgs innerArgs)
                {
                    Trace.WriteLine("Data was received correctly.");

                    Assert.AreEqual(data.Length, innerArgs.Bytes.Length);

                    for (int i = 0; i < data.Length; i++)
                    {
                        Assert.AreEqual(data[i], innerArgs.Bytes[i]);
                    }

                    Assert.AreEqual(sendOption, innerArgs.SendOption);

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

        /// <summary>
        ///     Builds new data of increaseing value bytes.
        /// </summary>
        /// <param name="dataSize">The number of bytes to generate.</param>
        /// <returns>The data.</returns>
        static byte[] BuildData(int dataSize)
        {
            byte[] data = new byte[dataSize];
            for (int i = 0; i < dataSize; i++)
                data[i] = (byte)i;
            return data;
        }
    }
}
