using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Hazel.Udp.FewerThreads
{
    /// <summary>
    ///     Listens for new UDP connections and creates UdpConnections for them.
    /// </summary>
    /// <inheritdoc />
    public class ThreadLimitedUdpConnectionListener : IDisposable
    {
        private struct SendMessageInfo
        {
            public byte[] Buffer;
            public EndPoint Recipient;
        }

        private struct ReceiveMessageInfo
        {
            public MessageReader Message;
            public EndPoint Sender;
        }

        private const int SendReceiveBufferSize = 1024 * 1024;
        private const int BufferSize = ushort.MaxValue;

        public event Action<NewConnectionEventArgs> NewConnection;

        /// <summary>
        /// A callback for early connection rejection. 
        /// * Return false to reject connection.
        /// * A null response is ok, we just won't send anything.
        /// </summary>
        public AcceptConnectionCheck AcceptConnection;
        public delegate bool AcceptConnectionCheck(IPEndPoint endPoint, byte[] input, out byte[] response);

        private Socket socket;
        private ILogger Logger;

        public IPEndPoint EndPoint { get; }
        public IPMode IPMode { get; }

        private Thread reliablePacketThread;
        private Thread receiveThread;
        private Thread sendThread;
        private HazelThreadPool processThreads;

        private ConcurrentDictionary<EndPoint, ThreadLimitedUdpServerConnection> allConnections = new ConcurrentDictionary<EndPoint, ThreadLimitedUdpServerConnection>();

        private BlockingCollection<ReceiveMessageInfo> receiveQueue;
        private Queue<SendMessageInfo> sendQueue = new Queue<SendMessageInfo>();

        public int MaxAge
        {
            get
            {
                var now = DateTime.UtcNow;
                TimeSpan max = new TimeSpan();
                foreach (var con in allConnections.Values)
                {
                    var val = now - con.CreationTime;
                    if (val > max) max = val;
                }

                return (int)max.TotalSeconds;
            }
        }

        public int ConnectionCount { get { return this.allConnections.Count; } }
        public int SendQueueLength { get { lock(this.sendQueue) return this.sendQueue.Count; } }
        public int ReceiveQueueLength { get { return this.receiveQueue.Count; } }

        private bool isActive;

        public ThreadLimitedUdpConnectionListener(int numWorkers, IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            this.Logger = logger;
            this.EndPoint = endPoint;
            this.IPMode = ipMode;

            this.receiveQueue = new BlockingCollection<ReceiveMessageInfo>(10000);

            this.socket = UdpConnection.CreateSocket(this.IPMode);
            this.socket.Blocking = false;

            this.socket.ReceiveBufferSize = SendReceiveBufferSize;
            this.socket.SendBufferSize = SendReceiveBufferSize;

            this.reliablePacketThread = new Thread(ManageReliablePackets);
            this.sendThread = new Thread(SendLoop);
            this.receiveThread = new Thread(ReceiveLoop);
            this.processThreads = new HazelThreadPool(numWorkers, ProcessingLoop);
        }

        ~ThreadLimitedUdpConnectionListener()
        {
            this.Dispose(false);
        }

        public void DisconnectOldConnections(TimeSpan maxAge, MessageWriter disconnectMessage)
        {
            var now = DateTime.UtcNow;
            foreach (var conn in this.allConnections.Values)
            {
                if (now - conn.CreationTime > maxAge)
                {
                    conn.Disconnect("Stale Connection", disconnectMessage);
                }
            }
        }
        
        private void ManageReliablePackets()
        {
            while (this.isActive)
            {
                foreach (var kvp in this.allConnections)
                {
                    var sock = kvp.Value;
                    sock.ManageReliablePackets();
                }

                Thread.Sleep(100);
            }
        }

        public void Start()
        {
            try
            {
                socket.Bind(EndPoint);
            }
            catch (SocketException e)
            {
                throw new HazelException("Could not start listening as a SocketException occurred", e);
            }

            this.isActive = true;
            this.reliablePacketThread.Start();
            this.sendThread.Start();
            this.receiveThread.Start();
            this.processThreads.Start();
        }

        private void ReceiveLoop()
        {
            while (this.isActive)
            {
                if (this.socket.Poll(Timeout.Infinite, SelectMode.SelectRead))
                {
                    EndPoint remoteEP = new IPEndPoint(this.EndPoint.Address, this.EndPoint.Port);
                    MessageReader message = MessageReader.GetSized(BufferSize);
                    try
                    {
                        message.Length = socket.ReceiveFrom(message.Buffer, 0, message.Buffer.Length, SocketFlags.None, ref remoteEP);
                    }
                    catch (SocketException sx)
                    {
                        message.Recycle();
                        this.Logger.WriteError("Socket Ex in StartListening: " + sx.Message);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        message.Recycle();
                        this.Logger.WriteError("Stopped due to: " + ex.Message);
                        return;
                    }

                    this.receiveQueue.Add(new ReceiveMessageInfo() { Message = message, Sender = remoteEP });
                }
            }
        }

        private void ProcessingLoop()
        {
            while (this.isActive)
            {
                ReceiveMessageInfo msg = this.receiveQueue.Take();
                
                try
                {
                    this.ReadCallback(msg.Message, msg.Sender);
                }
                catch
                {

                }
            }
        }

        private void SendLoop()
        {
            while (this.isActive)
            {
                SendMessageInfo msg;
                lock (this.sendQueue)
                {
                    if (this.sendQueue.Count == 0)
                    {
                        Monitor.Wait(this.sendQueue);

                        if (this.sendQueue.Count == 0)
                        {
                            continue;
                        }
                    }

                    msg = this.sendQueue.Dequeue();
                }

                try
                {
                    if (this.socket.Poll(Timeout.Infinite, SelectMode.SelectWrite))
                    {
                        this.socket.SendTo(msg.Buffer, 0, msg.Buffer.Length, SocketFlags.None, msg.Recipient);
                    }
                }
                catch (Exception e)
                {
                    this.Logger.WriteError("Error in loop while sending: " + e.Message);
                    Thread.Sleep(1);
                }
            }
        }

        void ReadCallback(MessageReader message, EndPoint remoteEndPoint)
        {
            int bytesReceived = message.Length;
            bool aware = true;
            bool isHello = message.Buffer[0] == (byte)UdpSendOption.Hello;

            // If we're aware of this connection use the one already
            // If this is a new client then connect with them!
            ThreadLimitedUdpServerConnection connection;
            if (!this.allConnections.TryGetValue(remoteEndPoint, out connection))
            {
                lock (this.allConnections)
                {
                    if (!this.allConnections.TryGetValue(remoteEndPoint, out connection))
                    {
                        // Check for malformed connection attempts
                        if (!isHello)
                        {
                            message.Recycle();
                            return;
                        }

                        if (AcceptConnection != null)
                        {
                            if (!AcceptConnection((IPEndPoint)remoteEndPoint, message.Buffer, out var response))
                            {
                                message.Recycle();
                                if (response != null)
                                {
                                    SendDataRaw(response, remoteEndPoint);
                                }

                                return;
                            }
                        }

                        aware = false;
                        connection = new ThreadLimitedUdpServerConnection(this, (IPEndPoint)remoteEndPoint, this.IPMode);
                        if (!this.allConnections.TryAdd(remoteEndPoint, connection))
                        {
                            throw new HazelException("Failed to add a connection. This should never happen.");
                        }
                    }
                }
            }

            // If it's a new connection invoke the NewConnection event.
            // This needs to happen before handling the message because in localhost scenarios, the ACK and
            // subsequent messages can happen before the NewConnection event sets up OnDataRecieved handlers
            if (!aware)
            {
                // Skip header and hello byte;
                message.Offset = 4;
                message.Length = bytesReceived - 4;
                message.Position = 0;
                this.NewConnection?.Invoke(new NewConnectionEventArgs(message, connection));
            }

            // Inform the connection of the buffer (new connections need to send an ack back to client)
            connection.HandleReceive(message, bytesReceived);

            if (isHello && aware)
            {
                message.Recycle();
            }
        }

        internal void SendDataRaw(byte[] response, EndPoint remoteEndPoint)
        {
            lock (this.sendQueue)
            {
                this.sendQueue.Enqueue(new SendMessageInfo() { Buffer = response, Recipient = remoteEndPoint });
                Monitor.Pulse(this.sendQueue);
            }
        }

        /// <summary>
        ///     Removes a virtual connection from the list.
        /// </summary>
        /// <param name="endPoint">The endpoint of the virtual connection.</param>
        internal bool RemoveConnectionTo(EndPoint endPoint)
        {
            return this.allConnections.TryRemove(endPoint, out var conn);
        }

        protected virtual void Dispose(bool disposing)
        {
            foreach (var kvp in this.allConnections)
            {
                kvp.Value.Dispose();
            }

            try { this.socket.Shutdown(SocketShutdown.Both); } catch { }
            try { this.socket.Close(); } catch { }
            try { this.socket.Dispose(); } catch { }

            this.isActive = false;

            lock (this.sendQueue) Monitor.PulseAll(this.sendQueue);

            this.receiveQueue.CompleteAdding();

            this.reliablePacketThread.Join();
            this.sendThread.Join();
            this.receiveThread.Join();
            this.processThreads.Join();
        }

        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}
