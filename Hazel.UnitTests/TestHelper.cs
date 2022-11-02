using Hazel.Udp.FewerThreads;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading;

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
        internal static void RunServerToClientTest(NetworkConnectionListener listener, Connection connection, int dataSize, SendOption sendOption)
        {
            //Setup meta stuff 
            MessageWriter data = BuildData(sendOption, dataSize);
            ManualResetEvent clientConnected = new ManualResetEvent(false);
            ManualResetEvent dataReceived = new ManualResetEvent(false);

            Connection conn = null;
            //Setup listener
            listener.NewConnection += delegate (NewConnectionEventArgs ncArgs)
            {
                conn = ncArgs.Connection;
                clientConnected.Set();
            };

            //Setup connection
            DataReceivedEventArgs? result = null;
            connection.DataReceived += delegate (DataReceivedEventArgs a)
            {
                Console.WriteLine("Data was received correctly.");
                result = a;
                dataReceived.Set();
            };

            listener.Start();
            connection.Connect();

            // Wait until data is received
            Assert.IsTrue(clientConnected.WaitOne(1000), "Client never connected");

            Assert.AreEqual(SendErrors.None, conn.Send(data));
            Console.WriteLine("Data was sent correctly.");

            // Wait until data is received
            Assert.IsTrue(dataReceived.WaitOne(1000), "Data was never received");

            var dataReader = ConvertToMessageReader(data);
            Assert.AreEqual(dataReader.Length, result.Value.Message.Length);
            for (int i = 0; i < dataReader.Length; i++)
            {
                Assert.AreEqual(dataReader.ReadByte(), result.Value.Message.ReadByte());
            }

            Assert.AreEqual(sendOption, result.Value.SendOption);
        }

        /// <summary>
        ///     Runs a general test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunClientToServerTest(NetworkConnectionListener listener, Connection connection, int dataSize, SendOption sendOption)
        {
            //Setup meta stuff 
            MessageWriter data = BuildData(sendOption, dataSize);
            ManualResetEvent mutex = new ManualResetEvent(false);
            ManualResetEvent mutex2 = new ManualResetEvent(false);

            //Setup listener
            DataReceivedEventArgs? result = null;
            listener.NewConnection += delegate (NewConnectionEventArgs args)
            {
                args.Connection.DataReceived += delegate (DataReceivedEventArgs innerArgs)
                {
                    Console.WriteLine("Data was received correctly.");
                    result = innerArgs;
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();
            connection.Connect();

            Assert.IsTrue(mutex.WaitOne(1000), "Client never connected");

            Assert.AreEqual(SendErrors.None, connection.Send(data));

            Assert.IsTrue(mutex2.WaitOne(1000), "Data was never received");

            var dataReader = ConvertToMessageReader(data);
            Assert.AreEqual(dataReader.Length, result.Value.Message.Length);
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(dataReader.ReadByte(), result.Value.Message.ReadByte());
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

            connection.Disconnected += delegate (object sender, DisconnectedEventArgs args)
            {
                mutex.Set();
            };

            listener.NewConnection += delegate (NewConnectionEventArgs args)
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

            listener.NewConnection += delegate (NewConnectionEventArgs args)
            {
                args.Connection.Disconnected += delegate (object sender2, DisconnectedEventArgs args2)
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
        ///     Ensures a client sends a disconnect packet to the server on Dispose.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunClientDisconnectOnDisposeTest(NetworkConnectionListener listener, Connection connection)
        {
            ManualResetEvent mutex = new ManualResetEvent(false);
            ManualResetEvent mutex2 = new ManualResetEvent(false);

            listener.NewConnection += delegate (NewConnectionEventArgs args)
            {
                args.Connection.Disconnected += delegate (object sender2, DisconnectedEventArgs args2)
                {
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            connection.Connect();

            if (!mutex.WaitOne(TimeSpan.FromSeconds(1)))
            {
                Assert.Fail("Timeout waiting for client connection");
            }

            connection.Dispose();

            if (!mutex2.WaitOne(TimeSpan.FromSeconds(1)))
            {
                Assert.Fail("Timeout waiting for client disconnect packet");
            }
        }

        private static MessageReader ConvertToMessageReader(MessageWriter writer)
        {
            var output = new MessageReader();
            output.Buffer = writer.Buffer;
            output.Offset = writer.SendOption == SendOption.None ? 1 : 3;
            output.Length = writer.Length - output.Offset;
            output.Position = 0;

            return output;
        }

        /// <summary>
        ///     Builds new data of increaseing value bytes.
        /// </summary>
        /// <param name="dataSize">The number of bytes to generate.</param>
        /// <returns>The data.</returns>
        static MessageWriter BuildData(SendOption sendOption, int dataSize)
        {
            var output = MessageWriter.Get(sendOption);
            for (int i = 0; i < dataSize; i++)
            {
                output.Write((byte)i);
            }

            return output;
        }
    }
}
