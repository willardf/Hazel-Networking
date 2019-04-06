/*
 *  BenchmarkNet is a console application for testing the reliable UDP networking solutions
 *  Copyright (c) 2018 Stanislav Denisov
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Net;
using System.Reflection;
using System.Runtime;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hazel;
using Hazel.Udp;

namespace NX
{
    public abstract class BenchmarkNet
    {
        // Meta
        public const string title = "BenchmarkNet";
        public const string version = "1.10";
        // Parameters
        public const string ip = "127.0.0.1";
        public static byte selectedLibrary = 0;
        public static ushort port = 0;
        public static ushort maxClients = 0;
        public static int serverTickRate = 0;
        public static int clientTickRate = 0;
        public static int sendRate = 0;
        public static int reliableMessages = 0;
        public static int unreliableMessages = 0;
        public static string message = String.Empty;
        // Status
        public static volatile bool processActive = false;
        public static volatile bool processCompleted = false;
        public static volatile bool processCrashed = false;
        public static volatile bool processFailure = false;
        public static volatile bool processOverload = false;
        public static volatile bool processUninitialized = true;
        // Stats
        public static volatile int serverReliableSent = 0;
        public static volatile int serverReliableReceived = 0;
        public static volatile int serverReliableBytesSent = 0;
        public static volatile int serverReliableBytesReceived = 0;
        public static volatile int serverUnreliableSent = 0;
        public static volatile int serverUnreliableReceived = 0;
        public static volatile int serverUnreliableBytesSent = 0;
        public static volatile int serverUnreliableBytesReceived = 0;
        public static volatile int clientsStartedCount = 0;
        public static volatile int clientsConnectedCount = 0;
        public static volatile int clientsStreamsCount = 0;
        public static volatile int clientsDisconnectedCount = 0;
        public static volatile int clientsReliableSent = 0;
        public static volatile int clientsReliableReceived = 0;
        public static volatile int clientsReliableBytesSent = 0;
        public static volatile int clientsReliableBytesReceived = 0;
        public static volatile int clientsUnreliableSent = 0;
        public static volatile int clientsUnreliableReceived = 0;
        public static volatile int clientsUnreliableBytesSent = 0;
        public static volatile int clientsUnreliableBytesReceived = 0;
        // Libraries
        public static readonly string[] networkingLibraries = {
            "ENet",
            "UNet",
            "LiteNetLib",
            "Lidgren",
            "MiniUDP",
            "Hazel",
            "Photon",
            "Neutrino",
            "DarkRift"
        };
        // Data
        protected static byte[] messageData;
        protected static byte[] reversedData;
        protected static char[] reversedMessage;
        // Internals
        private static bool serverInstance = false;
        private static bool clientsInstance = false;
        private static bool maxClientsPass = true;
        private static bool sustainedLowLatency = false;
        private static ushort maxPeers = 0;
        private static BinaryFormatter binaryFormatter;
        private static ServerMessage serverMessage;
        private static ClientsMessage clientsMessage;
        private static MemoryMappedViewStream serverStream;
        private static MemoryMappedViewStream clientsStream;
        private static NamedPipeServerStream serverPipe;
        private static NamedPipeServerStream clientsPipe;
        private static Process serverProcess;
        private static Process clientsProcess;
        private static Thread serverThread;
        private const int memoryMappedLength = 512;
        private const ushort defaultPort = 9500;
        private const ushort defaultMaxClients = 1000;
        private const int defaultServerTickRate = 64;
        private const int defaultClientTickRate = 64;
        private const int defaultSendRate = 15;
        private const int defaultReliableMessages = 500;
        private const int defaultUnreliableMessages = 1000;
        private const string defaultMessage = "Sometimes we just need a good networking library";
        // Functions
#if !GUI
        private static readonly Func<int, string> Space = (value) => String.Empty.PadRight(value);
        private static readonly Func<int, decimal, decimal, decimal> PayloadThroughput = (clientsStreamsCount, messageLength, sendRate) => (clientsStreamsCount * (messageLength * sendRate * 2) * 8 / (1000 * 1000));
#else
			
#endif

        [Serializable]
        private struct ServerMessage
        {
            public bool uninitialized;
            public int reliableSent;
            public int reliableReceived;
            public int reliableBytesSent;
            public int reliableBytesReceived;
            public int unreliableSent;
            public int unreliableReceived;
            public int unreliableBytesSent;
            public int unreliableBytesReceived;
        }

        [Serializable]
        private struct ClientsMessage
        {
            public int startedCount;
            public int connectedCount;
            public int streamsCount;
            public int disconnectedCount;
            public int reliableSent;
            public int reliableReceived;
            public int reliableBytesSent;
            public int reliableBytesReceived;
            public int unreliableSent;
            public int unreliableReceived;
            public int unreliableBytesSent;
            public int unreliableBytesReceived;
        }

        public static bool Initialize()
        {
            binaryFormatter = new BinaryFormatter();

            MemoryMappedFile serverData = MemoryMappedFile.CreateOrOpen(title + "ServerData", memoryMappedLength, MemoryMappedFileAccess.ReadWrite);

            serverStream = serverData.CreateViewStream(0, memoryMappedLength);
            binaryFormatter.Serialize(serverStream, serverMessage);
            serverStream.Position = 0;

            MemoryMappedFile clientsData = MemoryMappedFile.CreateOrOpen(title + "ClientsData", memoryMappedLength, MemoryMappedFileAccess.ReadWrite);

            clientsStream = clientsData.CreateViewStream(0, memoryMappedLength);
            binaryFormatter.Serialize(clientsStream, clientsMessage);
            clientsStream.Position = 0;

            if (serverInstance)
            {
                if (selectedLibrary == 5)
                    serverThread = new Thread(HazelBenchmark.Server);
                
                if (serverThread == null)
                    return false;
            }

            UInt16.TryParse(ConfigurationManager.AppSettings["Port"], out port);
            Int32.TryParse(ConfigurationManager.AppSettings["ServerTickRate"], out serverTickRate);
            Int32.TryParse(ConfigurationManager.AppSettings["ClientTickRate"], out clientTickRate);
            Int32.TryParse(ConfigurationManager.AppSettings["SendRate"], out sendRate);
            Int32.TryParse(ConfigurationManager.AppSettings["ReliableMessages"], out reliableMessages);
            Int32.TryParse(ConfigurationManager.AppSettings["UnreliableMessages"], out unreliableMessages);
            message = ConfigurationManager.AppSettings["Message"];
            Boolean.TryParse(ConfigurationManager.AppSettings["SustainedLowLatency"], out sustainedLowLatency);

            if (port == 0)
                port = defaultPort;

            if (maxClients == 0)
                maxClients = defaultMaxClients;

            if (serverTickRate == 0)
                serverTickRate = defaultServerTickRate;

            if (clientTickRate == 0)
                clientTickRate = defaultClientTickRate;

            if (sendRate == 0)
                sendRate = defaultSendRate;

            if (reliableMessages == 0)
                reliableMessages = defaultReliableMessages;

            if (unreliableMessages == 0)
                unreliableMessages = defaultUnreliableMessages;

            if (string.IsNullOrEmpty(message))
                message = defaultMessage;

            reversedMessage = message.ToCharArray();
            Array.Reverse(reversedMessage);
            messageData = Encoding.ASCII.GetBytes(message);
            reversedData = Encoding.ASCII.GetBytes(new string(reversedMessage));

#if !GUI
            Console.CursorVisible = false;
            Console.Clear();
#endif

            processActive = true;

            if (serverInstance)
            {
                if (sustainedLowLatency)
                    GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

                maxPeers = ushort.MaxValue - 1;
                maxClientsPass = maxClients <= maxPeers;

                if (!maxClientsPass)
                    maxClients = Math.Min(Math.Max((ushort)1, maxClients), maxPeers);

                serverThread.Priority = ThreadPriority.AboveNormal;
                serverThread.Start();
                Thread.Sleep(100);
            }

            if (!serverInstance && !clientsInstance)
            {
                serverProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = Assembly.GetExecutingAssembly().Location,
                    Arguments = "-library:" + selectedLibrary + " -server:" + maxClients,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                clientsProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = Assembly.GetExecutingAssembly().Location,
                    Arguments = "-library:" + selectedLibrary + " -clients:" + maxClients,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }

            Task pulseTask = Pulse();
            Task dataTask = serverInstance && selectedLibrary == Array.FindIndex(networkingLibraries, entry => entry.Contains("Photon")) ? null : Data();

#if !GUI
            Task infoTask = serverInstance || clientsInstance ? null : Info();
#endif

            Task superviseTask = serverInstance || clientsInstance ? null : Supervise();
            Task spawnTask = serverInstance || !clientsInstance ? null : Spawn();

            if (serverInstance)
                processUninitialized = false;

            return true;
        }

        private static void Deinitialize()
        {
            processActive = false;

            if (!serverProcess.HasExited)
                serverProcess.Kill();

            if (!clientsProcess.HasExited)
                clientsProcess.Kill();
        }

        [STAThread]
        private static void Main(string[] arguments)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i].ToLower();

                if (argument.Contains("-library"))
                    Byte.TryParse(argument.Substring(argument.LastIndexOf(":") + 1), out selectedLibrary);

                if (argument.Contains("-server"))
                {
                    serverInstance = true;
                    UInt16.TryParse(argument.Substring(argument.LastIndexOf(":") + 1), out maxClients);
                }

                if (argument.Contains("-clients"))
                {
                    clientsInstance = true;
                    UInt16.TryParse(argument.Substring(argument.LastIndexOf(":") + 1), out maxClients);
                }
            }

#if GUI
				
#else
            Console.Title = title;
            Console.SetIn(new StreamReader(Console.OpenStandardInput(8192), Console.InputEncoding, false, bufferSize: 1024));

            Start:

            if (!serverInstance && !clientsInstance)
            {
                Console.WriteLine("Welcome to " + title + Space(1) + version + "!");

                Console.WriteLine(Environment.NewLine + "Source code is available on GitHub (https://github.com/nxrighthere/BenchmarkNet)");
                Console.WriteLine("If you have any questions, contact me (nxrighthere@gmail.com)");

                if (sustainedLowLatency)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(Environment.NewLine + "The server process will perform in Sustained Low Latency mode.");
                    Console.ResetColor();
                }

                Console.WriteLine(Environment.NewLine + "Select the networking library:");

                for (int i = 0; i < networkingLibraries.Length; i++)
                {
                    Console.WriteLine("(" + i + ") " + networkingLibraries[i]);
                }

                Console.Write(Environment.NewLine + "Enter the number (default 0): ");
                Byte.TryParse(Console.ReadLine(), out selectedLibrary);

                if (selectedLibrary >= networkingLibraries.Length)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Please, enter a valid number of the networking library!");
                    Console.ResetColor();
                    Console.ReadKey();
                    Console.Clear();

                    goto Start;
                }

                Console.Write("Simulated clients (default " + defaultMaxClients + "): ");
                UInt16.TryParse(Console.ReadLine(), out maxClients);
            }

            if (!Initialize())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Initialization failed!");
            }

            Console.ReadKey();
#endif

            Deinitialize();

            Environment.Exit(0);
        }

        private static async Task Pulse()
        {
            await Task.Factory.StartNew(() =>
            {
                const string serverPipeName = title + "Server";
                const string clientsPipeName = title + "Clients";

                if (serverInstance)
                {
                    NamedPipeClientStream serverPipeStream = new NamedPipeClientStream(".", serverPipeName, PipeDirection.In);

                    serverPipeStream.Connect();
                    serverPipeStream.BeginRead(new byte[1], 0, 1, (result) => Process.GetCurrentProcess().Kill(), serverPipeStream);
                }
                else if (clientsInstance)
                {
                    NamedPipeClientStream clientsPipeStream = new NamedPipeClientStream(".", clientsPipeName, PipeDirection.In);

                    clientsPipeStream.Connect();
                    clientsPipeStream.BeginRead(new byte[1], 0, 1, (result) => Process.GetCurrentProcess().Kill(), clientsPipeStream);
                }
                else
                {
                    Task.Run(async () =>
                    {
                        serverPipe = new NamedPipeServerStream(serverPipeName, PipeDirection.Out);

                        await serverPipe.WaitForConnectionAsync();
                    });

                    Task.Run(async () =>
                    {
                        clientsPipe = new NamedPipeServerStream(clientsPipeName, PipeDirection.Out);

                        await clientsPipe.WaitForConnectionAsync();
                    });
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static async Task Data()
        {
            await Task.Factory.StartNew(() =>
            {
                bool monitoringInstance = !serverInstance && !clientsInstance;
                byte[] serverBuffer = !monitoringInstance ? null : new byte[memoryMappedLength];
                byte[] clientsBuffer = !monitoringInstance ? null : new byte[memoryMappedLength];
                MemoryStream serverMemory = !monitoringInstance ? null : new MemoryStream(serverBuffer);
                MemoryStream clientsMemory = !monitoringInstance ? null : new MemoryStream(clientsBuffer);

                while (processActive)
                {
                    if (serverInstance)
                    {
                        serverMessage.uninitialized = processUninitialized;
                        serverMessage.reliableSent = serverReliableSent;
                        serverMessage.reliableReceived = serverReliableReceived;
                        serverMessage.reliableBytesSent = serverReliableBytesSent;
                        serverMessage.reliableBytesReceived = serverReliableBytesReceived;
                        serverMessage.unreliableSent = serverUnreliableSent;
                        serverMessage.unreliableReceived = serverUnreliableReceived;
                        serverMessage.unreliableBytesSent = serverUnreliableBytesSent;
                        serverMessage.unreliableBytesReceived = serverUnreliableBytesReceived;

                        binaryFormatter.Serialize(serverStream, serverMessage);
                        serverStream.Position = 0;
                    }
                    else if (clientsInstance)
                    {
                        clientsMessage.startedCount = clientsStartedCount;
                        clientsMessage.connectedCount = clientsConnectedCount;
                        clientsMessage.streamsCount = clientsStreamsCount;
                        clientsMessage.disconnectedCount = clientsDisconnectedCount;
                        clientsMessage.reliableSent = clientsReliableSent;
                        clientsMessage.reliableReceived = clientsReliableReceived;
                        clientsMessage.reliableBytesSent = clientsReliableBytesSent;
                        clientsMessage.reliableBytesReceived = clientsReliableBytesReceived;
                        clientsMessage.unreliableSent = clientsUnreliableSent;
                        clientsMessage.unreliableReceived = clientsUnreliableReceived;
                        clientsMessage.unreliableBytesSent = clientsUnreliableBytesSent;
                        clientsMessage.unreliableBytesReceived = clientsUnreliableBytesReceived;

                        binaryFormatter.Serialize(clientsStream, clientsMessage);
                        clientsStream.Position = 0;
                    }
                    else
                    {
                        serverStream.Read(serverBuffer, 0, memoryMappedLength);
                        clientsStream.Read(clientsBuffer, 0, memoryMappedLength);

                        serverMessage = (ServerMessage)binaryFormatter.Deserialize(serverMemory);
                        processUninitialized = serverMessage.uninitialized;
                        serverReliableSent = serverMessage.reliableSent;
                        serverReliableReceived = serverMessage.reliableReceived;
                        serverReliableBytesSent = serverMessage.reliableBytesSent;
                        serverReliableBytesReceived = serverMessage.reliableBytesReceived;
                        serverUnreliableSent = serverMessage.unreliableSent;
                        serverUnreliableReceived = serverMessage.unreliableReceived;
                        serverUnreliableBytesSent = serverMessage.unreliableBytesSent;
                        serverUnreliableBytesReceived = serverMessage.unreliableBytesReceived;
                        serverMemory.Position = 0;

                        clientsMessage = (ClientsMessage)binaryFormatter.Deserialize(clientsMemory);
                        clientsStartedCount = clientsMessage.startedCount;
                        clientsConnectedCount = clientsMessage.connectedCount;
                        clientsStreamsCount = clientsMessage.streamsCount;
                        clientsDisconnectedCount = clientsMessage.disconnectedCount;
                        clientsReliableSent = clientsMessage.reliableSent;
                        clientsReliableReceived = clientsMessage.reliableReceived;
                        clientsReliableBytesSent = clientsMessage.reliableBytesSent;
                        clientsReliableBytesReceived = clientsMessage.reliableBytesReceived;
                        clientsUnreliableSent = clientsMessage.unreliableSent;
                        clientsUnreliableReceived = clientsMessage.unreliableReceived;
                        clientsUnreliableBytesSent = clientsMessage.unreliableBytesSent;
                        clientsUnreliableBytesReceived = clientsMessage.unreliableBytesReceived;
                        clientsMemory.Position = 0;

                        serverStream.Position = 0;
                        clientsStream.Position = 0;
                    }

                    Thread.Sleep(15);
                }
            }, TaskCreationOptions.LongRunning);
        }

#if !GUI
        private static async Task Info()
        {
            await Task.Factory.StartNew(() =>
            {
                int spinnerTimer = 0;
                int spinnerSequence = 0;
                string space = Space(10);
                string[] spinner = {
                        "/",
                        "—",
                        "\\",
                        "|"
                    };
                string[] status = {
                        "Running" + Space(6),
                        "Crashed" + Space(6),
                        "Failure" + Space(6),
                        "Overload" + Space(5),
                        "Completed" + Space(4),
                        "Uninitialized"
                    };
                string[] strings = {
                        "Benchmarking " + networkingLibraries[selectedLibrary] + "...",
                        "Server tick rate: " + serverTickRate + ", Client tick rate: " + clientTickRate + " (ticks per second)",
                        maxClients + " clients, " + reliableMessages + " reliable and " + unreliableMessages + " unreliable messages per client, " + sendRate + " messages per second, " + messageData.Length + " bytes per message",
                        "GC mode: " + (!GCSettings.IsServerGC ? "Workstation" : "Server"),
                        "This networking library doesn't support more than " + maxPeers  + " peers per server!",
                        "The server process is performing in Sustained Low Latency mode.",
                    };

                for (int i = 0; i < spinner.Length; i++)
                {
                    spinner[i] = Environment.NewLine + "Press any key to stop the process" + Space(1) + spinner[i];
                }

                Console.WriteLine(strings[0]);
                Console.WriteLine(strings[1]);
                Console.WriteLine(strings[2]);
                Console.WriteLine(strings[3]);

                StringBuilder info = new StringBuilder(1024);
                Stopwatch elapsedTime = Stopwatch.StartNew();

                while (processActive)
                {
                    Console.CursorVisible = false;
                    Console.SetCursorPosition(0, 4);

                    if (!maxClientsPass || sustainedLowLatency)
                        Console.WriteLine();

                    if (!maxClientsPass)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(strings[4]);
                        Console.ResetColor();
                    }

                    if (sustainedLowLatency)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(strings[5]);
                        Console.ResetColor();
                    }

                    info.Clear()
                    .AppendLine().Append("[Server]")
                    .AppendLine().Append("Status: ").Append(processCrashed ? status[1] : (processFailure ? status[2] : (processOverload ? status[3] : (processCompleted ? status[4] : (processUninitialized ? status[5] : status[0])))))
                    .AppendLine().Append("Sent -> Reliable: ").Append(serverReliableSent).Append(" messages (").Append(serverReliableBytesSent).Append(" bytes), Unreliable: ").Append(serverUnreliableSent).Append(" messages (").Append(serverUnreliableBytesSent).Append(" bytes)")
                    .AppendLine().Append("Received <- Reliable: ").Append(serverReliableReceived).Append(" messages (").Append(serverReliableBytesReceived).Append(" bytes), Unreliable: ").Append(serverUnreliableReceived).Append(" messages (").Append(serverUnreliableBytesReceived).Append(" bytes)")
                    .AppendLine().Append("Payload throughput: ").Append(PayloadThroughput(clientsStreamsCount, messageData.Length, sendRate).ToString("0.00")).Append(" mbps \\ ").Append(PayloadThroughput(maxClients * 2, messageData.Length, sendRate).ToString("0.00")).Append(" mbps").Append(space)
                    .AppendLine()
                    .AppendLine().Append("[Clients]")
                    .AppendLine().Append("Status: ").Append(clientsStartedCount).Append(" started, ").Append(clientsConnectedCount).Append(" connected, ").Append(clientsDisconnectedCount).Append(" dropped")
                    .AppendLine().Append("Sent -> Reliable: ").Append(clientsReliableSent).Append(" messages (").Append(clientsReliableBytesSent).Append(" bytes), Unreliable: ").Append(clientsUnreliableSent).Append(" messages (").Append(clientsUnreliableBytesSent).Append(" bytes)")
                    .AppendLine().Append("Received <- Reliable: ").Append(clientsReliableReceived).Append(" messages (").Append(clientsReliableBytesReceived).Append(" bytes), Unreliable: ").Append(clientsUnreliableReceived).Append(" messages (").Append(clientsUnreliableBytesReceived).Append(" bytes)")
                    .AppendLine()
                    .AppendLine().Append("[Summary]")
                    .AppendLine().Append("Total - Reliable: ").Append((ulong)clientsReliableSent + (ulong)serverReliableReceived + (ulong)serverReliableSent + (ulong)clientsReliableReceived).Append(" messages (").Append((ulong)clientsReliableBytesSent + (ulong)serverReliableBytesReceived + (ulong)serverReliableBytesSent + (ulong)clientsReliableBytesReceived).Append(" bytes), Unreliable: ").Append((ulong)clientsUnreliableSent + (ulong)serverUnreliableReceived + (ulong)serverUnreliableSent + (ulong)clientsUnreliableReceived).Append(" messages (").Append((ulong)clientsUnreliableBytesSent + (ulong)serverUnreliableBytesReceived + (ulong)serverUnreliableBytesSent + (ulong)clientsUnreliableBytesReceived).Append(" bytes)")
                    .AppendLine().Append("Expected - Reliable: ").Append(maxClients * (ulong)reliableMessages * 4).Append(" messages (").Append(maxClients * (ulong)reliableMessages * (ulong)messageData.Length * 4).Append(" bytes), Unreliable: ").Append(maxClients * (ulong)unreliableMessages * 4).Append(" messages (").Append(maxClients * (ulong)unreliableMessages * (ulong)messageData.Length * 4).Append(" bytes)")
                    .AppendLine().Append("Elapsed time: ").Append(elapsedTime.Elapsed.Hours.ToString("00")).Append(":").Append(elapsedTime.Elapsed.Minutes.ToString("00")).Append(":").Append(elapsedTime.Elapsed.Seconds.ToString("00"));

                    Console.WriteLine(info);

                    if (spinnerTimer >= 10)
                    {
                        spinnerSequence++;
                        spinnerTimer = 0;

                        if (spinnerSequence == spinner.Length)
                            spinnerSequence = 0;
                    }
                    else
                    {
                        spinnerTimer++;
                    }

                    Console.WriteLine(spinner[spinnerSequence]);
                    Thread.Sleep(1000 / 60);
                }

                elapsedTime.Stop();

                if (!processActive && processCompleted)
                {
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.WriteLine("Process completed! Press any key to exit...");
                }
            }, TaskCreationOptions.LongRunning);
        }
#endif

        private static async Task Supervise()
        {
            await Task.Factory.StartNew(() =>
            {
                decimal currentData = 0;
                decimal lastData = 0;
                bool recollectData = true;

                while (processActive)
                {
                    Thread.Sleep(1000);

                    Collect:

                    currentData = ((decimal)serverReliableSent + (decimal)serverReliableReceived + (decimal)serverUnreliableSent + (decimal)serverUnreliableReceived + (decimal)clientsReliableSent + (decimal)clientsReliableReceived + (decimal)clientsUnreliableSent + (decimal)clientsUnreliableReceived);

                    if (serverProcess.HasExited)
                        processCrashed = true;

                    if (currentData == lastData)
                    {
                        if (currentData == 0)
                        {
                            if (recollectData)
                            {
                                recollectData = false;
                                Thread.Sleep(4000);

                                goto Collect;
                            }

                            processFailure = true;
                        }
                        else if (clientsDisconnectedCount > 1 || ((currentData / (maxClients * ((decimal)reliableMessages + (decimal)unreliableMessages) * 4)) * 100) < 90)
                        {
                            processOverload = true;
                        }

                        processCompleted = true;
                        Thread.Sleep(100);

                        Deinitialize();

                        break;
                    }

                    lastData = currentData;
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static async Task Spawn()
        {
            await Task.Factory.StartNew(() =>
            {
                Task[] clients = new Task[maxClients];

                for (int i = 0; i < maxClients; i++)
                {
                    if (!processActive)
                        break;

                    clients[i] = HazelBenchmark.Client();

                    Interlocked.Increment(ref clientsStartedCount);
                    Thread.Sleep(15);
                }
            }, TaskCreationOptions.LongRunning);
        }
    }

    public sealed class HazelBenchmark : BenchmarkNet
    {
        public static void Server()
        {
            UdpConnectionListener server = new UdpConnectionListener(new IPEndPoint(IPAddress.Parse(ip), port));

            server.NewConnection += (netEvent) =>
            {
                netEvent.Connection.DataReceived += (evt) =>
                {
                    Connection client = evt.Sender;

                    if (evt.SendOption == SendOption.Reliable)
                    {
                        Interlocked.Increment(ref serverReliableReceived);
                        Interlocked.Add(ref serverReliableBytesReceived, evt.Message.Length);

                        if (client.State == Hazel.ConnectionState.Connected)
                        {
                            client.SendBytes(messageData, SendOption.Reliable);
                            Interlocked.Increment(ref serverReliableSent);
                            Interlocked.Add(ref serverReliableBytesSent, messageData.Length);
                        }
                    }
                    else if (evt.SendOption == SendOption.None)
                    {
                        Interlocked.Increment(ref serverUnreliableReceived);
                        Interlocked.Add(ref serverUnreliableBytesReceived, evt.Message.Length);

                        if (client.State == Hazel.ConnectionState.Connected)
                        {
                            client.SendBytes(reversedData, SendOption.None);
                            Interlocked.Increment(ref serverUnreliableSent);
                            Interlocked.Add(ref serverUnreliableBytesSent, reversedData.Length);
                        }
                    }

                    evt.Message.Recycle();
                };
            };

            server.Start();

            while (processActive)
            {
                Thread.Sleep(1000 / serverTickRate);
            }

            server.Close();
        }

        public static async Task Client()
        {
            await Task.Factory.StartNew(() =>
            {
                UdpClientConnection client = new UdpClientConnection(new IPEndPoint(IPAddress.Parse(ip), port));

                client.Connect();

                int reliableToSend = 0;
                int unreliableToSend = 0;

                Task.Factory.StartNew(async () =>
                {
                    bool reliableIncremented = false;
                    bool unreliableIncremented = false;

                    while (processActive)
                    {
                        if (reliableToSend > 0)
                        {
                            client.SendBytes(messageData, SendOption.Reliable);
                            Interlocked.Decrement(ref reliableToSend);
                            Interlocked.Increment(ref clientsReliableSent);
                            Interlocked.Add(ref clientsReliableBytesSent, messageData.Length);
                        }

                        if (unreliableToSend > 0)
                        {
                            client.SendBytes(reversedData, SendOption.None);
                            Interlocked.Decrement(ref unreliableToSend);
                            Interlocked.Increment(ref clientsUnreliableSent);
                            Interlocked.Add(ref clientsUnreliableBytesSent, reversedData.Length);
                        }

                        if (reliableToSend > 0 && !reliableIncremented)
                        {
                            reliableIncremented = true;
                            Interlocked.Increment(ref clientsStreamsCount);
                        }
                        else if (reliableToSend == 0 && reliableIncremented)
                        {
                            reliableIncremented = false;
                            Interlocked.Decrement(ref clientsStreamsCount);
                        }

                        if (unreliableToSend > 0 && !unreliableIncremented)
                        {
                            unreliableIncremented = true;
                            Interlocked.Increment(ref clientsStreamsCount);
                        }
                        else if (unreliableToSend == 0 && unreliableIncremented)
                        {
                            unreliableIncremented = false;
                            Interlocked.Decrement(ref clientsStreamsCount);
                        }

                        await Task.Delay(1000 / sendRate);
                    }
                }, TaskCreationOptions.AttachedToParent);

                client.Disconnected += (sender, data) =>
                {
                    Interlocked.Increment(ref clientsDisconnectedCount);
                    Interlocked.Exchange(ref reliableToSend, 0);
                    Interlocked.Exchange(ref unreliableToSend, 0);
                };

                client.DataReceived += (data) =>
                {
                    if (data.SendOption == SendOption.Reliable)
                    {
                        Interlocked.Increment(ref clientsReliableReceived);
                        Interlocked.Add(ref clientsReliableBytesReceived, data.Message.Length);
                    }
                    else if (data.SendOption == SendOption.None)
                    {
                        Interlocked.Increment(ref clientsUnreliableReceived);
                        Interlocked.Add(ref clientsUnreliableBytesReceived, data.Message.Length);
                    }

                    data.Message.Recycle();
                };

                bool connected = false;

                while (processActive)
                {
                    if (!connected && client.State == Hazel.ConnectionState.Connected)
                    {
                        connected = true;
                        Interlocked.Increment(ref clientsConnectedCount);
                        Interlocked.Exchange(ref reliableToSend, reliableMessages);
                        Interlocked.Exchange(ref unreliableToSend, unreliableMessages);
                    }

                    Thread.Sleep(1000 / clientTickRate);
                }

                client.Disconnect("Client stop.");
            }, TaskCreationOptions.LongRunning);
        }
    }
}