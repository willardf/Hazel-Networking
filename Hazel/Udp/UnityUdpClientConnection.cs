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
        private Socket socket;
        protected readonly ILogger logger;

        public UnityUdpClientConnection(ILogger logger, IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4)
            : base()
        {
            this.logger = logger;
            this.EndPoint = remoteEndPoint;
            this.IPMode = ipMode;

            this.socket = CreateSocket(ipMode);
            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
        }
        
        ~UnityUdpClientConnection()
        {
            this.Dispose(false);
        }

        public void FixedUpdate()
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
                ManageReliablePackets();
            }
            catch (Exception e)
            {
                this.logger.WriteError("FixedUpdate: " + e);
            }
        }

        protected virtual void RestartConnection()
        {
        }

        protected virtual void ResendPacketsIfNeeded()
        {
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnection(byte[] bytes, int length)
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

        private void WriteBytesToConnectionReal(byte[] bytes, int length)
        {
            try
            {
                this.Statistics.LogPacketSend(length);
                socket.BeginSendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    EndPoint,
                    HandleSendTo,
                    null);
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
        }

        /// <summary>
        ///     Synchronously writes the given bytes to the connection.
        /// </summary>
        /// <param name="bytes">The bytes to write.</param>
        protected virtual void WriteBytesToConnectionSync(byte[] bytes, int length)
        {
            try
            {
                socket.SendTo(
                    bytes,
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
        }

        public override void Connect(byte[] bytes = null, int timeout = 5000)
        {
            this.ConnectAsync(bytes);
            for(int timer = 0; timer < timeout; timer += 100)
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
            var msg = MessageReader.GetSized(ushort.MaxValue);
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

            var bytes = EmptyDisconnectBytes;
            if (data != null && data.Length > 0)
            {
                if (data.SendOption != SendOption.None) throw new ArgumentException("Disconnect messages can only be unreliable.");

                bytes = data.ToByteArray(true);
                bytes[0] = (byte)UdpSendOption.Disconnect;
            }

            try
            {
                this.WriteBytesToConnectionSync(bytes, bytes.Length);
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
