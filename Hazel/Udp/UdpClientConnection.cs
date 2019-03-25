using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


namespace Hazel.Udp
{
    /// <summary>
    ///     Represents a client's connection to a server that uses the UDP protocol.
    /// </summary>
    /// <inheritdoc/>
    public sealed class UdpClientConnection : UdpConnection
    {
        /// <summary>
        ///     The socket we're connected via.
        /// </summary>
        private Socket socket;

        /// <summary>
        ///     The buffer to store incomming data in.
        /// </summary>
        private byte[] dataBuffer = new byte[ushort.MaxValue];

        private Timer reliablePacketTimer;

        /// <summary>
        ///     Creates a new UdpClientConnection.
        /// </summary>
        /// <param name="remoteEndPoint">A <see cref="NetworkEndPoint"/> to connect to.</param>
        public UdpClientConnection(IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4)
            : base()
        {
            this.EndPoint = remoteEndPoint;
            this.RemoteEndPoint = remoteEndPoint;
            this.IPMode = ipMode;

            if (this.IPMode == IPMode.IPv4)
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new InvalidOperationException("IPV6 not supported!");

                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);    //TODO these lines shouldn't be needed anymore
            }

            reliablePacketTimer = new Timer(ManageReliablePacketsInternal, null, 100, Timeout.Infinite);
        }
        
        ~UdpClientConnection()
        {
            this.Dispose(false);
        }

        private void ManageReliablePacketsInternal(object state)
        {
            base.ManageReliablePackets();
            reliablePacketTimer.Change(100, Timeout.Infinite);
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnection(byte[] bytes, int length)
        {
            if (TestLagMs > 0)
            {
                ThreadPool.QueueUserWorkItem(a => { Thread.Sleep(this.TestLagMs); WriteBytesToConnectionReal(bytes, length); });
            }
            else
            {
                WriteBytesToConnectionReal(bytes, length);
            }
        }

        public event Action<byte[], int> DataSentRaw;
        public event Action<byte[]> DataReceivedRaw;

        private void WriteBytesToConnectionReal(byte[] bytes, int length)
        {
            DataSentRaw?.Invoke(bytes, length);

            try
            {
                socket.BeginSendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    RemoteEndPoint,
                    delegate (IAsyncResult result)
                    {
                        try
                        {
                            socket.EndSendTo(result);
                        }
                        catch (ObjectDisposedException)
                        {
                            Disconnect("Could not send as the socket was disposed of.");
                        }
                        catch (SocketException)
                        {
                            Disconnect("Could not send data as a SocketException occured.");
                        }
                    },
                    null
                );
            }
            catch (ObjectDisposedException)
            {
                //User probably called Disconnect in between this method starting and here so report the issue
                throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");
            }
            catch (SocketException)
            {
                Disconnect("Could not send data as a SocketException occured.");
                throw;
            }
        }

        protected override void WriteBytesToConnectionSync(byte[] bytes, int length)
        {
            DataSentRaw?.Invoke(bytes, length);

            try
            {
                socket.SendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    RemoteEndPoint);
            }
            catch (ObjectDisposedException)
            {
                //User probably called Disconnect in between this method starting and here so report the issue
                throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");
            }
            catch (SocketException)
            {
                Disconnect("Could not send data as a SocketException occured.");
                throw;
            }
        }

        /// <inheritdoc />
        public override void Connect(byte[] bytes = null, int timeout = 5000)
        {
            this.ConnectAsync(bytes, timeout);

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
        public override void ConnectAsync(byte[] bytes = null, int timeout = 5000)
        {
            this.State = ConnectionState.Connecting;

            //Begin listening
            try
            {
                if (IPMode == IPMode.IPv4)
                    socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                else
                    socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
            }
            catch (SocketException e)
            {
                State = ConnectionState.NotConnected;
                throw new HazelException("A socket exception occured while binding to the port.", e);
            }

            try
            {
                StartListeningForData();
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there but make sure we're in NotConnected state.
                //If we end up here I'm really lost...
                State = ConnectionState.NotConnected;
                return;
            }
            catch (SocketException e)
            {
                Dispose();
                throw new HazelException("A Socket exception occured while initiating a receive operation.", e);
            }

            //Write bytes to the server to tell it hi (and to punch a hole in our NAT, if present)
            //When acknowledged set the state to connected
            SendHello(bytes, () => { State = ConnectionState.Connected; });
        }

        /// <summary>
        ///     Instructs the listener to begin listening.
        /// </summary>
        void StartListeningForData()
        {
            socket.BeginReceive(dataBuffer, 0, dataBuffer.Length, SocketFlags.None, ReadCallback, dataBuffer);
        }

        /// <summary>
        ///     Called when data has been received by the socket.
        /// </summary>
        /// <param name="result">The asyncronous operation's result.</param>
        void ReadCallback(IAsyncResult result)
        {
            int bytesReceived;

            //End the receive operation
            try
            {
                bytesReceived = socket.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }
            catch (SocketException e)
            {
                Disconnect("Socket exception while reading data: " + e.Message);
                return;
            }

            //Exit if no bytes read, we've failed.
            if (bytesReceived == 0)
            {
                Disconnect("Received 0 bytes");
                return;
            }

            //Copy data to new array
            byte[] bytes = new byte[bytesReceived];
            Buffer.BlockCopy(dataBuffer, 0, bytes, 0, bytesReceived);

            //Begin receiving again
            try
            {
                StartListeningForData();
            }
            catch (SocketException e)
            {
                Disconnect("Socket exception during receive: " + e.Message);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }

            if (this.TestLagMs > 0)
            {
                Thread.Sleep(this.TestLagMs);
            }

            if (this.TestDropRate > 0)
            {
                if ((this.testDropCount++ % this.TestDropRate) == 0)
                {
                    return;
                }
            }

            DataReceivedRaw?.Invoke(bytes);
            MessageReader msg = MessageReader.GetRaw(bytes, 0, bytesReceived);
            HandleReceive(msg, bytesReceived);
        }

        /// <summary>
        ///     Sends a disconnect message to the end point.
        /// </summary>
        protected override void SendDisconnect()
        {
            try
            {
                WriteBytesToConnectionSync(DisconnectBytes, 1);
            }
            catch { }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this._state == ConnectionState.Connected
                    || this._state == ConnectionState.Disconnecting)
                {
                    SendDisconnect();
                    this._state = ConnectionState.NotConnected;
                }
            }

            if (this.socket != null)
            {
                try { this.socket.Shutdown(SocketShutdown.Both); } catch { }
                try { this.socket.Close(); } catch { }
                try { this.socket.Dispose(); } catch { }

                this.socket = null;
            }

            this.reliablePacketTimer.Dispose();

            base.Dispose(disposing);
        }
    }
}