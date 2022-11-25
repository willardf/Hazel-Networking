using Hazel.Udp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Hazel.Dtls
{
    /// <summary>
    ///     Represents a client's connection to a server that uses the UDP protocol.
    /// </summary>
    /// <inheritdoc/>
    public partial class ThreadLimitedDtlsUnityConnection : UdpConnection
    {
        private const int BufferSize = ushort.MaxValue;
        protected override bool DisposeOnDisconnect => false;

        /// <summary>
        ///     The socket we're connected via.
        /// </summary>
        private Socket socket;

        /// <summary>
        ///     Reset event that is triggered when the connection is marked Connected.
        /// </summary>
        private ManualResetEvent connectWaitLock = new ManualResetEvent(false);

        private Thread receiveThread;
        private Thread sendThread;
        private Thread workerThread;

        private BlockingCollection<MessageReader> receiveQueue = new BlockingCollection<MessageReader>();
        private BlockingCollection<ByteSpan> sendQueue = new BlockingCollection<ByteSpan>();

        private Timer reliablePacketTimer;

        /// <summary>
        ///     Creates a new UdpClientConnection.
        /// </summary>
        /// <param name="remoteEndPoint">A <see cref="NetworkEndPoint"/> to connect to.</param>
        public ThreadLimitedDtlsUnityConnection(ILogger logger, IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4)
            : base(logger)
        {
            this.EndPoint = remoteEndPoint;
            this.IPMode = ipMode;

            this.nextEpoch.ServerRandom = new byte[Random.Size];
            this.nextEpoch.ClientRandom = new byte[Random.Size];
            this.nextEpoch.ServerVerification = new byte[Finished.Size];
            this.nextEpoch.CertificateFragments = new List<FragmentRange>();

            this.ResetConnectionState();

            this.socket = CreateSocket(ipMode);

            reliablePacketTimer = new Timer(ManageReliablePacketsInternal, null, Timeout.Infinite, Timeout.Infinite);
        }
        
        ~ThreadLimitedDtlsUnityConnection()
        {
            this.Dispose(false);
        }

        private void ManageReliablePacketsInternal(object state)
        {
            base.ManageReliablePackets();
            ResendPacketsIfNeeded();

            try
            {
                reliablePacketTimer.Change(100, Timeout.Infinite);
            }
            catch { }
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnection(byte[] bytes, int length)
        {
            var wireBytes = CreateApplicationDataRecord(bytes, length);

#if DEBUG
            if (TestLagMs > 0)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(this.TestLagMs); 
                    this.sendQueue.Add(wireBytes);
                });
            }
            else
#endif
            {
                this.sendQueue.Add(wireBytes);
            }
        }

        private void ReceiveLoop()
        {
            while (!this.receiveQueue.IsCompleted)
            {
                if (this.socket.Poll(1000, SelectMode.SelectRead))
                {
                    if (this.receiveQueue.IsCompleted) break;

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
                            // this.InvokeInternalError(HazelInternalErrors.ConnectionDisconnected);
                            return;
                        }

                        this.logger.WriteError("Socket Ex in ReceiveLoop: " + sx.Message);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        message.Recycle();
                        this.logger.WriteError("Stopped due to: " + ex.Message);
                        return;
                    }

                    this.receiveQueue.TryAdd(message);
                }
            }
        }

        private void WorkerLoop()
        {
            foreach (var msg in this.receiveQueue.GetConsumingEnumerable())
            {
                HandleReceive(msg, msg.Length);
            }
        }

        private void SendLoop()
        {
            foreach (var span in this.sendQueue.GetConsumingEnumerable())
            {
                this.Statistics.LogPacketSend(span.Length);

                try
                {
                    if (this.socket.Poll(1000, SelectMode.SelectWrite))
                    {
                        this.socket.SendTo(span.GetUnderlyingArray(), span.Offset, span.Length, SocketFlags.None, this.EndPoint);
                    }
                }
                catch (Exception e)
                {
                    this.logger.WriteError("Error in loop while sending: " + e.Message);
                    Thread.Sleep(1);
                }
            }
        }

        /// <inheritdoc />
        public override void Connect(byte[] bytes = null, int timeout = 5000)
        {
            this.ConnectAsync(bytes);

            //Wait till hello packet is acknowledged and the state is set to Connected
            bool timedOut = !WaitOnConnect(timeout);

            //If we timed out raise an exception
            if (timedOut)
            {
                Dispose();
                throw new HazelException("Connection attempt timed out.");
            }
        }

        /// <inheritdoc />
        public override void ConnectAsync(byte[] bytes = null)
        {
            this.State = ConnectionState.Connecting;

            try
            {
                if (IPMode == IPMode.IPv4)
                    socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                else
                    socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
            }
            catch (SocketException e)
            {
                this.State = ConnectionState.NotConnected;
                throw new HazelException("A SocketException occurred while binding to the port.", e);
            }

            this.receiveThread = new Thread(ReceiveLoop);
            this.receiveThread.Start();
            this.sendThread = new Thread(SendLoop);
            this.sendThread.Start();
            this.workerThread = new Thread(WorkerLoop);
            this.workerThread.Start();

            this.RestartConnection();
            this.InitializeKeepAliveTimer();

            // Write bytes to the server to tell it hi (and to punch a hole in our NAT, if present)
            // When acknowledged set the state to connected
            SendHello(bytes, () =>
            {
                this.State = ConnectionState.Connected;
            });
        }

        protected override void SetState(ConnectionState state)
        {
            try
            {
                // If the server disconnects you during the hello
                // you can go straight from Connecting to NotConnected.
                if (state == ConnectionState.Connected
                    || state == ConnectionState.Disconnected)
                {
                    connectWaitLock.Set();
                }
                else
                {
                    connectWaitLock.Reset();
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        ///     Blocks until the Connection is connected.
        /// </summary>
        /// <param name="timeout">The number of milliseconds to wait before timing out.</param>
        public bool WaitOnConnect(int timeout)
        {
            return connectWaitLock.WaitOne(timeout);
        }

        /// <summary>
        ///     Sends a disconnect message to the end point.
        ///     You may include optional disconnect data. The SendOption must be unreliable.
        /// </summary>
        protected override bool SendDisconnect(MessageWriter data = null)
        {
            lock (this)
            {
                if (this._state == ConnectionState.NotConnected || this._state == ConnectionState.Disconnected) return false;
                this.State = ConnectionState.Disconnected; // Use the property so we release the state lock
            }

            var bytes = EmptyDisconnectBytes;
            if (data != null && data.Length > 0)
            {
                if (data.SendOption != SendOption.None) throw new ArgumentException("Disconnect messages can only be unreliable.");

                bytes = data.ToByteArray(true);
                bytes[0] = (byte)UdpSendOption.Disconnect;
            }

            try
            {
                WriteBytesToConnection(bytes, bytes.Length);
            }
            catch
            {

            }

            return true;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SendDisconnect();
            }

            this.reliablePacketTimer.Dispose();
            this.connectWaitLock.Dispose();

            if (disposing)
            {
                this.receiveQueue.CompleteAdding();
                this.receiveThread?.Join();
                this.workerThread?.Join();

                this.sendQueue.CompleteAdding();
                this.sendThread?.Join();
            }

            try { this.socket.Shutdown(SocketShutdown.Both); } catch { }
            try { this.socket.Close(); } catch { }
            try { this.socket.Dispose(); } catch { }

            lock (this.syncRoot)
            {
                this.ResetConnectionState();
            }

            base.Dispose(disposing);
        }
    }
}
