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
        Socket socket;

        /// <summary>
        ///     The buffer to store incomming data in.
        /// </summary>
        byte[] dataBuffer = new byte[ushort.MaxValue];

        Timer reliablePacketTimer;

        /// <summary>
        ///     Creates a new UdpClientConnection.
        /// </summary>
        /// <param name="remoteEndPoint">A <see cref="NetworkEndPoint"/> to connect to.</param>
        public UdpClientConnection(NetworkEndPoint remoteEndPoint)
            : base()
        {
            this.EndPoint = remoteEndPoint;
            this.RemoteEndPoint = remoteEndPoint.EndPoint;
            this.IPMode = remoteEndPoint.IPMode;

            if (remoteEndPoint.IPMode == IPMode.IPv4)
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new HazelException("IPV6 not supported!");

                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);    //TODO these lines shouldn't be needed anymore
            }

            reliablePacketTimer = new Timer((s) => ManageReliablePackets(s), null, 50, Timeout.Infinite);
        }

        ~UdpClientConnection()
        {
            this.Dispose(false);
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

        private void WriteBytesToConnectionReal(byte[] bytes, int length)
        {
            InvokeDataSentRaw(bytes, length);

            if (State != ConnectionState.Connected && State != ConnectionState.Connecting)
                throw new InvalidOperationException("Could not send data as this Connection is not connected and is not connecting. Did you disconnect?");

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
                            HandleDisconnect("Could not send as the socket was disposed of.");
                        }
                        catch (SocketException)
                        {
                            HandleDisconnect("Could not send data as a SocketException occured.");
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
                HandleDisconnect("Could not send data as a SocketException occured.");
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
            if (State != ConnectionState.NotConnected)
                throw new InvalidOperationException("Cannot connect as the Connection is already connected.");

            State = ConnectionState.Connecting;

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
                HandleDisconnect("A socket exception occured while reading data.");
                return;
            }

            //Exit if no bytes read, we've failed.
            if (bytesReceived == 0)
            {
                HandleDisconnect("Recieved 0 bytes");
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
                HandleDisconnect("A Socket exception occured while initiating a receive operation.");
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

            MessageReader msg = MessageReader.GetRaw(bytes, 0, bytesReceived);
            HandleReceive(msg, bytesReceived);
        }

        /// <inheritdoc />
        protected override void HandleDisconnect(string e)
        {
            if (State == ConnectionState.Connected)
            {
                State = ConnectionState.Disconnecting;

                try
                {
                    InvokeDisconnected(e);
                }
                catch { }
            }

            Dispose();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //Send disconnect message if we're not already disconnecting
                if (State == ConnectionState.Connected)
                {
                    State = ConnectionState.NotConnected;
                    try
                    {
                        SendDisconnect();
                    }
                    catch { }
                }
            }

            if (socket != null)
            {
                socket.Close();
                socket.Dispose();
                socket = null;
            }

            this.reliablePacketTimer.Dispose();

            base.Dispose(disposing);
        }
    }
}