using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace Hazel.Udp
{
    /// <summary>
    /// Unity doesn't always get along with thread pools well, so this interface will hopefully suit that case better.
    /// </summary>
    /// <inheritdoc/>
    public class UnityUdpClientConnection : UdpConnection
    {
        /// <summary>
        /// The max size Hazel attempts to read from the network.
        /// Defaults to 8096.
        /// </summary>
        /// <remarks>
        /// 8096 is 5 times the standard modern MTU of 1500, so it's already too large imo.
        /// If Hazel ever implements fragmented packets, then we might consider a larger value since combining 5 
        /// packets into 1 reader would be realistic and would cause reallocations. That said, Hazel is not meant
        /// for transferring large contiguous blocks of data, so... please don't?
        /// </remarks>
        public int ReceiveBufferSize = 8096;

        private Socket socket;

        public UnityUdpClientConnection(ILogger logger, IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4)
            : base(logger)
        {
            this.EndPoint = remoteEndPoint;
            this.IPMode = ipMode;

            this.socket = CreateSocket(ipMode);
            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
        }
        
        ~UnityUdpClientConnection()
        {
            this.Dispose(false);
        }

        public int FixedUpdate()
        {
            try
            {
                ResendPacketsIfNeeded();
            }
            catch (Exception e)
            {
                this.logger.WriteError("FixedUpdate: " + e);
            }

            try
            {
                return ManageReliablePackets();
            }
            catch (Exception e)
            {
                this.logger.WriteError("FixedUpdate: " + e);
            }

            return 0;
        }

        protected virtual void RestartConnection()
        {
        }

        protected virtual void ResendPacketsIfNeeded()
        {
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnection(SmartBuffer bytes, int length)
        {
#if DEBUG
            if (TestLagMs > 0)
            {
                ThreadPool.QueueUserWorkItem(a => { Thread.Sleep(this.TestLagMs); WriteBytesToConnectionReal(bytes, length); });
            }
            else
#endif
            {
                WriteBytesToConnectionReal(bytes, length);
            }
        }

        private void WriteBytesToConnectionReal(SmartBuffer bytes, int length)
        {
            try
            {
                bytes.AddUsage();
                this.Statistics.LogPacketSend(length);
                socket.BeginSendTo(
                    (byte[])bytes,
                    0,
                    length,
                    SocketFlags.None,
                    EndPoint,
                    HandleSendTo,
                    bytes);
            }
            catch (NullReferenceException)
            {
                bytes.Recycle();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed and disconnected...
                bytes.Recycle();
            }
            catch (SocketException ex)
            {
                bytes.Recycle();
                DisconnectInternal(HazelInternalErrors.SocketExceptionSend, "Could not send data as a SocketException occurred: " + ex.Message);
            }
        }

        /// <summary>
        ///     Synchronously writes the given bytes to the connection.
        /// </summary>
        /// <param name="bytes">The bytes to write.</param>
        protected virtual void WriteBytesToConnectionSync(SmartBuffer bytes, int length)
        {
            bytes.AddUsage();
            try
            {
                socket.SendTo(
                    (byte[])bytes,
                    0,
                    length,
                    SocketFlags.None,
                    EndPoint);
            }
            catch (NullReferenceException) { }
            catch (ObjectDisposedException)
            {
                // Already disposed and disconnected...
            }
            catch (SocketException ex)
            {
                DisconnectInternal(HazelInternalErrors.SocketExceptionSend, "Could not send data as a SocketException occurred: " + ex.Message);
            }
            finally
            {
                bytes.Recycle();
            }
        }

        private void HandleSendTo(IAsyncResult result)
        {
            try
            {
                socket.EndSendTo(result);
            }
            catch (NullReferenceException) { }
            catch (ObjectDisposedException)
            {
                // Already disposed and disconnected...
            }
            catch (SocketException ex)
            {
                DisconnectInternal(HazelInternalErrors.SocketExceptionSend, "Could not send data as a SocketException occurred: " + ex.Message);
            }
            finally
            {
                ((SmartBuffer)result.AsyncState).Recycle();
            }
        }

        public override void Connect(byte[] bytes = null, int timeout = 5000)
        {
            this.ConnectAsync(bytes);
            for (int timer = 0; timer < timeout; timer += 100)
            {
                if (this.State != ConnectionState.Connecting) return;
                Thread.Sleep(100);

                // I guess if we're gonna block in Unity, then let's assume no one will pump this for us.
                this.FixedUpdate();
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

            this.RestartConnection();

            try
            {
                StartListeningForData();
            }
            catch (ObjectDisposedException)
            {
                // If the socket's been disposed then we can just end there but make sure we're in NotConnected state.
                // If we end up here I'm really lost...
                this.State = ConnectionState.NotConnected;
                return;
            }
            catch (SocketException e)
            {
                Dispose();
                throw new HazelException("A SocketException occurred while initiating a receive operation.", e);
            }

            // Write bytes to the server to tell it hi (and to punch a hole in our NAT, if present)
            // When acknowledged set the state to connected
            SendHello(bytes, () =>
            {
                this.InitializeKeepAliveTimer();
                this.State = ConnectionState.Connected;
            });
        }

        /// <summary>
        ///     Instructs the listener to begin listening.
        /// </summary>
        void StartListeningForData()
        {
            var msg = MessageReader.GetSized(this.ReceiveBufferSize);
            try
            {
                EndPoint ep = this.EndPoint;
                socket.BeginReceiveFrom(msg.Buffer, 0, msg.Buffer.Length, SocketFlags.None, ref ep, ReadCallback, msg);
            }
            catch
            {
                msg.Recycle();
                this.Dispose();
            }
        }

        /// <summary>
        ///     Called when data has been received by the socket.
        /// </summary>
        /// <param name="result">The asyncronous operation's result.</param>
        void ReadCallback(IAsyncResult result)
        {
#if DEBUG
            if (this.TestLagMs > 0)
            {
                Thread.Sleep(this.TestLagMs);
            }
#endif

            var msg = (MessageReader)result.AsyncState;

            try
            {
                EndPoint ep = this.EndPoint;
                msg.Length = socket.EndReceiveFrom(result, ref ep);
            }
            catch (SocketException e)
            {
                msg.Recycle();
                DisconnectInternal(HazelInternalErrors.SocketExceptionReceive, "Socket exception while reading data: " + e.Message);
                return;
            }
            catch (ObjectDisposedException)
            {
                // Weirdly, it seems that this method can be called twice on the same AsyncState when object is disposed...
                // So this just keeps us from hitting Duplicate Add errors at the risk of if this is a platform
                // specific bug, we leak a MessageReader while the socket is disposing. Not a bad trade off.
                return;
            }
            catch (Exception)
            {
                msg.Recycle();
                return;
            }

            //Exit if no bytes read, we've failed.
            if (msg.Length == 0)
            {
                msg.Recycle();
                DisconnectInternal(HazelInternalErrors.ReceivedZeroBytes, "Received 0 bytes");
                return;
            }

            //Begin receiving again
            try
            {
                StartListeningForData();
            }
            catch (SocketException e)
            {
                DisconnectInternal(HazelInternalErrors.SocketExceptionReceive, "Socket exception during receive: " + e.Message);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }

#if DEBUG
            if (this.TestDropRate > 0)
            {
                if ((this.testDropCount++ % this.TestDropRate) == 0)
                {
                    return;
                }
            }
#endif

            HandleReceive(msg, msg.Length);
        }

        /// <summary>
        ///     Sends a disconnect message to the end point.
        ///     You may include optional disconnect data. The SendOption must be unreliable.
        /// </summary>
        protected override bool SendDisconnect(MessageWriter data = null)
        {
            lock (this)
            {
                if (this._state == ConnectionState.NotConnected) return false;
                this._state = ConnectionState.NotConnected;
            }

            using SmartBuffer buffer = this.bufferPool.GetObject();
            buffer.CopyFrom(EmptyDisconnectBytes);

            if (data != null && data.Length > 0)
            {
                if (data.SendOption != SendOption.None) throw new ArgumentException("Disconnect messages can only be unreliable.");

                buffer.CopyFrom(data);
                buffer[0] = (byte)UdpSendOption.Disconnect;
            }

            try
            {
                this.WriteBytesToConnectionSync(buffer, buffer.Length);
            }
            catch { }

            return true;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SendDisconnect();
            }

            try { this.socket.Shutdown(SocketShutdown.Both); } catch { }
            try { this.socket.Close(); } catch { }
            try { this.socket.Dispose(); } catch { }

            base.Dispose(disposing);
        }
    }
}
