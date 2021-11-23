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
            public ByteSpan Span;
            public IPEndPoint Recipient;
        }

        private struct ReceiveMessageInfo
        {
            public MessageReader Message;
            public IPEndPoint Sender;
            public ConnectionId ConnectionId;
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
        protected ILogger Logger;

        public IPEndPoint EndPoint { get; }
        public IPMode IPMode { get; }

        private Thread reliablePacketThread;
        private Thread receiveThread;
        private Thread sendThread;
        private HazelThreadPool processThreads;

        public bool ReceiveThreadRunning => this.receiveThread.ThreadState == ThreadState.Running;

        public struct ConnectionId : IEquatable<ConnectionId>
        {
            public IPEndPoint EndPoint;
            public int Serial;

            public static ConnectionId Create(IPEndPoint endPoint, int serial)
            {
                return new ConnectionId{
                    EndPoint = endPoint,
                    Serial = serial,
                };
            }

            public bool Equals(ConnectionId other)
            {
                return this.Serial == other.Serial
                    && this.EndPoint.Equals(other.EndPoint)
                    ;
            }

            public override bool Equals(object obj)
            {
                if (obj is ConnectionId)
                {
                    return this.Equals((ConnectionId)obj);
                }

                return false;
            }

            public override int GetHashCode()
            {
                ///NOTE(mendsley): We're only hashing the endpoint
                /// here, as the common case will have one
                /// connection per address+port tuple.
                return this.EndPoint.GetHashCode();
            }
        }

        protected ConcurrentDictionary<ConnectionId, ThreadLimitedUdpServerConnection> allConnections = new ConcurrentDictionary<ConnectionId, ThreadLimitedUdpServerConnection>();
        
        private BlockingCollection<ReceiveMessageInfo> receiveQueue;
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

        public ThreadLimitedUdpConnectionListener(int numWorkers, IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            this.Logger = logger;
            this.EndPoint = endPoint;
            this.IPMode = ipMode;

            this.receiveQueue = new BlockingCollection<ReceiveMessageInfo>(10000);

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

        ~ThreadLimitedUdpConnectionListener()
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
                        this.Logger.WriteError("Socket Ex in ReceiveLoop: " + sx.Message);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        message.Recycle();
                        this.Logger.WriteError("Stopped due to: " + ex.Message);
                        return;
                    }

                    ConnectionId connectionId = ConnectionId.Create((IPEndPoint)remoteEP, 0);
                    this.ProcessIncomingMessageFromOtherThread(message, (IPEndPoint)remoteEP, connectionId);
                }
            }
        }

        private void ProcessingLoop()
        {
            foreach (ReceiveMessageInfo msg in this.receiveQueue.GetConsumingEnumerable())
            {
                try
                {
                    this.ReadCallback(msg.Message, msg.Sender, msg.ConnectionId);
                }
                catch
                {

                }
            }
        }

        protected void ProcessIncomingMessageFromOtherThread(MessageReader message, IPEndPoint remoteEndPoint, ConnectionId connectionId)
        {
            this.receiveQueue.Add(new ReceiveMessageInfo() { Message = message, Sender = remoteEndPoint, ConnectionId = connectionId });
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

        protected virtual void ReadCallback(MessageReader message, IPEndPoint remoteEndPoint, ConnectionId connectionId)
        {
            int bytesReceived = message.Length;
            bool aware = true;
            bool isHello = message.Buffer[0] == (byte)UdpSendOption.Hello;

            // If we're aware of this connection use the one already
            // If this is a new client then connect with them!
            ThreadLimitedUdpServerConnection connection;
            if (!this.allConnections.TryGetValue(connectionId, out connection))
            {
                lock (this.allConnections)
                {
                    if (!this.allConnections.TryGetValue(connectionId, out connection))
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
                        connection = new ThreadLimitedUdpServerConnection(this, connectionId, (IPEndPoint)remoteEndPoint, this.IPMode);
                        if (!this.allConnections.TryAdd(connectionId, connection))
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
                try
                {
                    this.NewConnection?.Invoke(new NewConnectionEventArgs(message, connection));
                }
                catch (Exception e)
                {
                    this.Logger.WriteError("NewConnection handler threw: " + e);
                }
            }

            // Inform the connection of the buffer (new connections need to send an ack back to client)
            connection.HandleReceive(message, bytesReceived);
        }

        internal void SendDataRaw(byte[] response, IPEndPoint remoteEndPoint)
        {
            QueueRawData(response, remoteEndPoint);
        }

        protected virtual void QueueRawData(ByteSpan span, IPEndPoint remoteEndPoint)
        {
            this.sendQueue.TryAdd(new SendMessageInfo() { Span = span, Recipient = remoteEndPoint });
        }

        /// <summary>
        ///     Removes a virtual connection from the list.
        /// </summary>
        /// <param name="endPoint">Connection key of the virtual connection.</param>
        internal bool RemoveConnectionTo(ConnectionId connectionId)
        {
            return this.allConnections.TryRemove(connectionId, out _);
        }

        /// <summary>
        ///  This is after all messages could be sent. Clean up anything extra.
        /// </summary>
        internal virtual void RemovePeerRecord(ConnectionId connectionId)
        {
        }

        protected virtual void Dispose(bool disposing)
        {
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

            this.receiveQueue?.CompleteAdding();

            if (wasActive)
            {
                this.reliablePacketThread.Join();
                this.receiveThread.Join();
                this.processThreads.Join();
            }

            this.receiveQueue?.Dispose();
            this.receiveQueue = null;
            this.sendQueue?.Dispose();
            this.sendQueue = null;
        }

        public void Dispose()
        {
            this.Dispose(true);
        }
    }
}
