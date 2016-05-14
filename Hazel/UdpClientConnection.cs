using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hazel
{
    public class UdpClientConnection : UdpConnection
    {
        /// <summary>
        ///     The socket we're connected via.
        /// </summary>
        Socket socket;

        /// <summary>
        ///     The buffer to store incomming data in.
        /// </summary>
        byte[] dataBuffer = new byte[ushort.MaxValue];

        /// <summary>
        ///     Creates a new UdpClientConnection.
        /// </summary>
        public UdpClientConnection()
            : base()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        }

        /// <summary>
        ///     Writes an array of bytes to the connection.
        /// </summary>
        /// <param name="bytes">The bytes of the message to send.</param>
        /// <param name="sendOption">The option this data is requested to send with.</param>
        public override void WriteBytes(byte[] bytes, SendOption sendOption = SendOption.None)
        {
            if (State != ConnectionState.Connected)
                throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");

            //Add header information and send
            HandleSend(bytes, (byte)sendOption);
        }

        /// <summary>
        ///     Writes bytes to the socket.
        /// </summary>
        /// <param name="bytes">The bytes to send.</param>
        protected override void WriteBytesToConnection(byte[] bytes)
        {
            //Pack
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.SetBuffer(bytes, 0, bytes.Length);
            args.RemoteEndPoint = RemoteEndPoint;

            lock (socket)
            {
                if (State != ConnectionState.Connected && State != ConnectionState.Connecting)
                    throw new InvalidOperationException("Could not send data as this Connection is not connected and is not connecting. Did you disconnect?");

                try
                {
                    socket.SendToAsync(args);
                }
                catch (ObjectDisposedException)
                {
                    //User probably called Disconnect in between this method starting and here so report the issue
                    throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");
                }
                catch (SocketException e)
                {
                    HazelException he = new HazelException("Could not send data as a SocketException occured.", e);
                    HandleDisconnect(he);
                    throw he;
                }
            }
        }

        /// <summary>
        ///     Connects this Connection to a given remote server and begins listening for data.
        /// </summary>
        public override void Connect(ConnectionEndPoint remoteEndPoint)
        {
            NetworkEndPoint nep = remoteEndPoint as NetworkEndPoint;
            if (nep == null)
            {
                throw new ArgumentException("The remote end point of a UDP connection must be a NetworkEndPoint.");
            }

            this.EndPoint = nep;
            this.RemoteEndPoint = nep.EndPoint;

            lock (socket)
            {
                if (State != ConnectionState.NotConnected)
                    throw new InvalidOperationException("Cannot connect as the Connection is already connected.");

                State = ConnectionState.Connecting;

                //Calculate local end point
                EndPoint localEndPoint;
                if (nep.EndPoint is IPEndPoint)
                    localEndPoint = new IPEndPoint(((IPEndPoint)nep.EndPoint).Address, 0);
                else if (nep.EndPoint is IPEndPoint)
                    localEndPoint = new DnsEndPoint(((DnsEndPoint)nep.EndPoint).Host, 0);
                else
                    throw new ArgumentException("Can only connect using an IPEndPoint or DnsEndpoint");

                //Begin listening
                try
                {
                    socket.Bind(localEndPoint);
                }
                catch (SocketException e)
                {
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
                    throw new HazelException("A Socket exception occured while initiating a receive operation.", e);
                }
            }

            //Write bytes to the server to tell it hi (and to punch a hole in our NAT, if present)
            //When acknowledged set the state to connected
            SendHello(() => State = ConnectionState.Connected);

            //Wait till hello packet is acknowledged and the state is set to Connected
            WaitOnConnect();
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
                lock (socket)
                    bytesReceived = socket.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }
            catch (SocketException e)
            {
                HandleDisconnect(new HazelException("A socket exception occured while reading data.", e));
                return;
            }

            //Exit if no bytes read, we've failed.
            if (bytesReceived == 0)
            {
                HandleDisconnect();
                return;
            }

            //Decode the data received
            byte[] buffer = HandleReceive(dataBuffer, bytesReceived);
            SendOption sendOption = (SendOption)dataBuffer[0];

            //TODO may get better performance with Handle receive after and block copy call added

            //Begin receiving again
            try
            {
                lock (socket)
                    StartListeningForData();
            }
            catch (SocketException e)
            {
                HandleDisconnect(new HazelException("A Socket exception occured while initiating a receive operation.", e));
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }
            
            if (buffer != null)
                InvokeDataReceived(new DataEventArgs(buffer, sendOption));
        }

        /// <summary>
        ///     Called when the socket has been disconnected at the remote host.
        /// </summary>
        /// <param name="e">The exception if one was the cause.</param>
        protected override void HandleDisconnect(HazelException e = null)
        {
            bool invoke = false;

            lock (socket)
            {
                //Only invoke the disconnected event if we're not already disconnecting
                if (State == ConnectionState.Connected)
                {
                    State = ConnectionState.Disconnecting;
                    invoke = true;
                }
            }

            //Invoke event outide lock if need be
            if (invoke)
            {
                InvokeDisconnected(new DisconnectedEventArgs(e));

                Dispose();
            }
        }

        /// <summary>
        ///     Safely closes this connection.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            //Dispose of the socket
            if (disposing)
            {
                lock (socket)
                {
                    State = ConnectionState.NotConnected;

                    socket.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
