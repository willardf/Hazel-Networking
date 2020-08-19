using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Hazel;
using System.Net;
using System.Threading;
using System.Diagnostics;
using Hazel.Udp.FewerThreads;

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
        internal static void RunServerToClientTest(ThreadLimitedUdpConnectionListener listener, Connection connection, int dataSize, SendOption sendOption)
        {
            //Setup meta stuff 
            byte[] data = BuildData(dataSize);
            ManualResetEvent mutex = new ManualResetEvent(false);

            //Setup listener
            listener.NewConnection += delegate (NewConnectionEventArgs ncArgs)
            {
                ncArgs.Connection.SendBytes(data, sendOption);
            };

            listener.Start();

            DataReceivedEventArgs? args = null;
            //Setup conneciton
            connection.DataReceived += delegate (DataReceivedEventArgs a)
            {
                Trace.WriteLine("Data was received correctly.");

                try
                {
                    args = a;
                }
                finally
                {
                    mutex.Set();
                }
            };

            connection.Connect();

            //Wait until data is received
            mutex.WaitOne();

            Assert.AreEqual(data.Length, args.Value.Message.Length);

            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(data[i], args.Value.Message.ReadByte());
            }

            Assert.AreEqual(sendOption, args.Value.SendOption);
        }

        /// <summary>
        ///     Runs a general test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunServerToClientTest(NetworkConnectionListener listener, Connection connection, int dataSize, SendOption sendOption)
        {
            //Setup meta stuff 
            byte[] data = BuildData(dataSize);
            ManualResetEvent mutex = new ManualResetEvent(false);

            //Setup listener
            listener.NewConnection += delegate(NewConnectionEventArgs ncArgs)
            {
                ncArgs.Connection.SendBytes(data, sendOption);
            };

            listener.Start();

            DataReceivedEventArgs? args = null;
            //Setup conneciton
            connection.DataReceived += delegate(DataReceivedEventArgs a)
            {
                Trace.WriteLine("Data was received correctly.");

                try
                {
                    args = a;
                }
                finally
                {
                    mutex.Set();
                }
            };

            connection.Connect();

            //Wait until data is received
            mutex.WaitOne();

            Assert.AreEqual(data.Length, args.Value.Message.Length);

            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(data[i], args.Value.Message.ReadByte());
            }

            Assert.AreEqual(sendOption, args.Value.SendOption);
        }

        /// <summary>
        ///     Runs a general test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunClientToServerTest(NetworkConnectionListener listener, Connection connection, int dataSize, SendOption sendOption)
        {
            //Setup meta stuff 
            byte[] data = BuildData(dataSize);
            ManualResetEvent mutex = new ManualResetEvent(false);
            ManualResetEvent mutex2 = new ManualResetEvent(false);

            //Setup listener
            DataReceivedEventArgs? result = null;
            listener.NewConnection += delegate(NewConnectionEventArgs args)
            {
                args.Connection.DataReceived += delegate(DataReceivedEventArgs innerArgs)
                {
                    Trace.WriteLine("Data was received correctly.");

                    result = innerArgs;

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

            Assert.AreEqual(data.Length, result.Value.Message.Length);

            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(data[i], result.Value.Message.ReadByte());
            }

            Assert.AreEqual(sendOption, result.Value.SendOption);
        }


        /// <summary>
        ///     Runs a general test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunClientToServerTest(ThreadLimitedUdpConnectionListener listener, Connection connection, int dataSize, SendOption sendOption)
        {
            //Setup meta stuff 
            byte[] data = BuildData(dataSize);
            ManualResetEvent mutex = new ManualResetEvent(false);
            ManualResetEvent mutex2 = new ManualResetEvent(false);

            //Setup listener
            DataReceivedEventArgs? result = null;
            listener.NewConnection += delegate (NewConnectionEventArgs args)
            {
                args.Connection.DataReceived += delegate (DataReceivedEventArgs innerArgs)
                {
                    Trace.WriteLine("Data was received correctly.");

                    result = innerArgs;

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

            Assert.AreEqual(data.Length, result.Value.Message.Length);

            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(data[i], result.Value.Message.ReadByte());
            }

            Assert.AreEqual(sendOption, result.Value.SendOption);
        }

        /// <summary>
        ///     Runs a server disconnect test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunServerDisconnectTest(NetworkConnectionListener listener, Connection connection)
        {
            ManualResetEvent mutex = new ManualResetEvent(false);

            connection.Disconnected += delegate(object sender, DisconnectedEventArgs args)
            {
                mutex.Set();
            };

            listener.NewConnection += delegate(NewConnectionEventArgs args)
            {
                args.Connection.Disconnect("Testing");
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
        internal static void RunClientDisconnectTest(NetworkConnectionListener listener, Connection connection)
        {
            ManualResetEvent mutex = new ManualResetEvent(false);
            ManualResetEvent mutex2 = new ManualResetEvent(false);

            listener.NewConnection += delegate(NewConnectionEventArgs args)
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

            connection.Disconnect("Testing");

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
