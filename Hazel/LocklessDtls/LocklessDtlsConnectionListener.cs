using Hazel.Tools;
using Hazel.Udp;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace Hazel.Dtls
{
    /// <summary>
    ///     Listens for new UDP connections and creates UdpConnections for them.
    /// </summary>
    /// <inheritdoc />
    public partial class LocklessDtlsConnectionListener : NetworkConnectionListener
    {
        private struct SendMessageInfo
        {
            public ByteSpan Span;
            public IPEndPoint Recipient;

            public SendMessageInfo(ByteSpan packet, IPEndPoint remoteEndPoint)
            {
                this.Span = packet;
                this.Recipient = remoteEndPoint;
            }
        }

        private struct ReceiveMessageInfo
        {
            public MessageReader Message;
            public LocklessDtlsServerConnection Sender;

            public ReceiveMessageInfo(MessageReader message, LocklessDtlsServerConnection sender)
            {
                this.Message = message;
                this.Sender = sender;
            }
        }

        private const int SendReceiveBufferSize = 1024 * 1024;
        private const int BufferSize = ushort.MaxValue;

        /// <summary>
        /// A callback for early connection rejection. 
        /// * Return false to reject connection.
        /// * A null response is ok, we just won't send anything.
        /// </summary>
        public AcceptConnectionCheck AcceptConnection;
        public delegate bool AcceptConnectionCheck(IPEndPoint endPoint, byte[] input, out byte[] response);

        private Socket socket;
        protected ILogger Logger;

        private Thread reliablePacketThread;
        private Thread receiveThread;
        private Thread sendThread;
        private HazelThreadPool processThreads;

        public bool ReceiveThreadRunning => this.receiveThread.ThreadState == ThreadState.Running;

        protected ConcurrentDictionary<EndPoint, LocklessDtlsServerConnection> allConnections = new ConcurrentDictionary<EndPoint, LocklessDtlsServerConnection>();
        
        private ConcurrentSetQueue<LocklessDtlsServerConnection> receiveQueue;
        private BlockingCollection<SendMessageInfo> sendQueue = new BlockingCollection<SendMessageInfo>();

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
        public int SendQueueLength { get { return this.sendQueue.Count; } }
        public int ReceiveQueueLength { get { return this.receiveQueue.Count; } }

        private bool isActive;

        public LocklessDtlsConnectionListener(int numWorkers, IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            this.Logger = logger;
            this.EndPoint = endPoint;
            this.IPMode = ipMode;

            this.random = RandomNumberGenerator.Create();

            this.hmacHelper = new ThreadedHmacHelper(logger);

            this.receiveQueue = new ConcurrentSetQueue<LocklessDtlsServerConnection>();

            this.socket = UdpConnection.CreateSocket(this.IPMode);
            this.socket.ExclusiveAddressUse = true;
            this.socket.Blocking = false;

            this.socket.ReceiveBufferSize = SendReceiveBufferSize;
            this.socket.SendBufferSize = SendReceiveBufferSize;

            this.reliablePacketThread = new Thread(ManageReliablePackets);
            this.sendThread = new Thread(SendLoop);
            this.receiveThread = new Thread(ReceiveLoop);
            this.processThreads = new HazelThreadPool(numWorkers, ProcessingLoop);
        }

        ~LocklessDtlsConnectionListener()
        {
            this.Dispose(false);
        }

        // This is just for booting people after they've been connected a certain amount of time...
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
            TimeSpan maxAge = TimeSpan.FromSeconds(2.5f);
            DateTime now = DateTime.UtcNow;

            while (this.isActive)
            {
                foreach (var connection in this.allConnections.Values)
                {
                    connection.ManageReliablePackets();

                    var peer = connection.PeerData;
                    if (peer != null)
                    {
                        if (peer.Epoch == 0 || peer.NextEpoch.State != HandshakeState.ExpectingHello)
                        {
                            TimeSpan negotiationAge = now - peer.StartOfNegotiation;
                            if (negotiationAge > maxAge
                                && RemoveConnectionTo(connection.EndPoint))
                            {
                                connection.Disconnect("Stale Connection", null);
                                connection.Dispose();
                            }
                        }
                    }
                }
            }

            Thread.Sleep(100);
        }

        public override void Start()
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
                if (this.socket.Poll(1000, SelectMode.SelectRead))
                {
                    if (!isActive) break;

                    EndPoint remoteEP = new IPEndPoint(this.EndPoint.Address, this.EndPoint.Port);
                    MessageReader message = MessageReader.GetSized(BufferSize);
                    try
                    {
                        message.Length = socket.ReceiveFrom(message.Buffer, 0, message.Buffer.Length, SocketFlags.None, ref remoteEP);
                    }
                    catch (SocketException sx)
                    {
                        message.Recycle();
                        if (sx.SocketErrorCode == SocketError.NotConnected)
                        {
                            this.InvokeInternalError(HazelInternalErrors.ConnectionDisconnected);
                            return;
                        }

                        this.Logger.WriteError("Socket Ex in ReceiveLoop: " + sx.Message);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        message.Recycle();
                        this.Logger.WriteError("Stopped due to: " + ex.Message);
                        return;
                    }

                    LocklessDtlsServerConnection connection;
                    if (!this.allConnections.TryGetValue(remoteEP, out connection))
                    {
                        lock (this.allConnections)
                        {
                            if (!this.allConnections.TryGetValue(remoteEP, out connection))
                            {
                                connection = new LocklessDtlsServerConnection(this, (IPEndPoint)remoteEP, this.IPMode, this.Logger);
                                if (!this.allConnections.TryAdd(remoteEP, connection))
                                {
                                    this.Logger.WriteError("Failed to add unique connection! This should never happen!");
                                }
                            }
                        }
                    }

                    EnqueueMessageReceived(message, connection);
                }
            }
        }

        internal void EnqueueMessageReceived(MessageReader message, LocklessDtlsServerConnection connection)
        {
            connection.PacketsReceived.Enqueue(message);
            this.receiveQueue.TryAdd(connection);
        }

        private void ProcessingLoop()
        {
            var myTid = Thread.CurrentThread.ManagedThreadId;
            while (this.receiveQueue.TryTake(out var sender))
            {
                if (Interlocked.CompareExchange(ref sender.LockedBy, myTid, 0) != 0)
                {
                    continue;
                }

                while (sender.PacketsReceived.TryDequeue(out var message))
                {
                    try
                    {
                        this.ReadCallback(message, sender);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.WriteError("Unhandled exception in ReadCallback: " + ex);
                    }
                }

                while (sender.PacketsSent.TryDequeue(out var message))
                {
                    try
                    {
                        EncryptAndSendAppData(message, sender);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.WriteError("Unhandled exception in ReadCallback: " + ex);
                    }
                }

                Interlocked.Exchange(ref sender.LockedBy, 0);
            }
        }

        private void SendLoop()
        {
            foreach (SendMessageInfo msg in this.sendQueue.GetConsumingEnumerable())
            {
                try
                {
                    if (this.socket.Poll(Timeout.Infinite, SelectMode.SelectWrite))
                    {
                        this.socket.SendTo(msg.Span.GetUnderlyingArray(), msg.Span.Offset, msg.Span.Length, SocketFlags.None, msg.Recipient);
                    }
                    else
                    {
                        this.Logger.WriteError("Socket is no longer able to send");
                        break;
                    }
                }
                catch (Exception e)
                {
                    this.Logger.WriteError("Error in loop while sending: " + e.Message);
                    Thread.Sleep(1);
                }
            }
        }

        /// <summary>
        /// Handle an incoming datagram from the network.
        ///
        /// This is primarily a wrapper around ProcessIncomingMessage
        /// to ensure `reader.Recycle()` is always called
        /// </summary>
        private void ReadCallback(MessageReader reader, LocklessDtlsServerConnection connection)
        {
            try
            {
                ByteSpan message = new ByteSpan(reader.Buffer, reader.Offset + reader.Position, reader.BytesRemaining);
                this.ProcessIncomingMessage(message, connection);
            }
            finally
            {
                reader.Recycle();
            }
        }

        private void HandleApplicationData(MessageReader message, LocklessDtlsServerConnection connection)
        {
            int bytesReceived = message.Length;
            bool aware = connection.HelloProcessed;
            bool isHello = message.Buffer[0] == (byte)UdpSendOption.Hello;

            // If we're aware of this connection use the one already
            // If this is a new client then connect with them!

            if (!aware)
            {
                // Check for malformed connection attempts
                if (!isHello)
                {
                    message.Recycle();
                    return;
                }

                if (AcceptConnection != null)
                {
                    if (!AcceptConnection(connection.EndPoint, message.Buffer, out var response))
                    {
                        message.Recycle();
                        if (response != null)
                        {
                            EncryptAndSendAppData(response, connection);
                        }

                        return;
                    }
                }

                // If it's a new connection invoke the NewConnection event.
                // This needs to happen before handling the message because in localhost scenarios, the ACK and
                // subsequent messages can happen before the NewConnection event sets up OnDataRecieved handlers
                connection.SetHelloAsProcessed();

                // Skip header and hello byte;
                message.Offset = 4;
                message.Length = bytesReceived - 4;
                message.Position = 0;
                try
                {
                    this.InvokeNewConnection(message, connection);
                }
                catch (Exception e)
                {
                    this.Logger.WriteError("NewConnection handler threw: " + e);
                }
            }

            // Inform the connection of the buffer (new connections need to send an ack back to client)
            connection.HandleReceive(message, bytesReceived);
        }

        internal void QueuePlaintextAppData(byte[] response, LocklessDtlsServerConnection connection)
        {
            var myTid = Thread.CurrentThread.ManagedThreadId;
            if (connection.LockedBy == myTid)
            {
                // We are already dedicated to this connection, so let's just send instead of queueing and reading it later.
                EncryptAndSendAppData(response, connection);
            }
            else
            {
                connection.PacketsSent.Enqueue(response);
                this.receiveQueue.TryAdd(connection);
            }
        }

        /// <summary>
        ///     Removes a virtual connection from the list.
        /// </summary>
        /// <param name="endPoint">Connection key of the virtual connection.</param>
        internal bool RemoveConnectionTo(IPEndPoint endpoint)
        {
            return this.allConnections.TryRemove(endpoint, out _);
        }

        protected override void Dispose(bool disposing)
        {
            // Stop receiving messages and drain the queue
            var rcvQueue = this.receiveQueue;
            if (rcvQueue != null)
            {
                rcvQueue.CompleteAdding();
                while (rcvQueue.TryTake(out _)) ;
            }

            foreach (var kvp in this.allConnections)
            {
                kvp.Value.Dispose();
            }

            bool wasActive = this.isActive;
            this.isActive = false;

            // Flush outgoing packets
            this.sendQueue?.CompleteAdding();

            if (wasActive)
            {
                this.sendThread.Join();
            }

            try { this.socket.Shutdown(SocketShutdown.Both); } catch { }
            try { this.socket.Close(); } catch { }
            try { this.socket.Dispose(); } catch { }

            if (wasActive)
            {
                this.reliablePacketThread.Join();
                this.receiveThread.Join();
                this.processThreads.Join();
            }

            // this.receiveQueue?.Dispose();
            this.receiveQueue = null;
            this.sendQueue?.Dispose();
            this.sendQueue = null;

            this.random?.Dispose();
            this.random = null;

            this.hmacHelper?.Dispose();
            this.hmacHelper = null;

            base.Dispose(disposing);
        }
    }
}
