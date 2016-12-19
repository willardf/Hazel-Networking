using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Hazel.Tcp
{
    /// <summary>
    ///     Represents a connection that uses the TCP protocol.
    /// </summary>
    /// <inheritdoc />
    public sealed class TcpConnection : NetworkConnection
    {
        /// <summary>
        ///     The socket we're managing.
        /// </summary>
        Socket socket;

        /// <summary>
        ///     Lock for the socket.
        /// </summary>
        Object socketLock = new Object();

        /// <summary>
        ///     Creates a TcpConnection from a given TCP Socket.
        /// </summary>
        /// <param name="socket">The TCP socket to wrap.</param>
        internal TcpConnection(Socket socket)
        {
            //Check it's a TCP socket
            if (socket.ProtocolType != System.Net.Sockets.ProtocolType.Tcp)
                throw new ArgumentException("A TcpConnection requires a TCP socket.");

            lock (this.socketLock)
            {
                this.EndPoint = new NetworkEndPoint(socket.RemoteEndPoint);
                this.RemoteEndPoint = socket.RemoteEndPoint;

                this.socket = socket;
                this.socket.NoDelay = true;

                State = ConnectionState.Connected;
            }
        }

        /// <summary>
        ///     Creates a new TCP connection.
        /// </summary>
        /// <param name="remoteEndPoint">A <see cref="NetworkEndPoint"/> to connect to.</param>
        public TcpConnection(NetworkEndPoint remoteEndPoint)
        {
            lock (socketLock)
            {
                if (State != ConnectionState.NotConnected)
                    throw new InvalidOperationException("Cannot connect as the Connection is already connected.");

                this.EndPoint = remoteEndPoint;
                this.RemoteEndPoint = remoteEndPoint.EndPoint;
                this.IPMode = remoteEndPoint.IPMode;

                //Create a socket
                if (remoteEndPoint.IPMode == IPMode.IPv4)
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                else
                {
                    if (!Socket.OSSupportsIPv6)
                        throw new HazelException("IPV6 not supported!");

                    socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                    socket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
                }

                socket.NoDelay = true;
            }
        }

        /// <inheritdoc />
        public override void Connect(byte[] bytes = null, int timeout = 5000)
        {
            lock(socketLock)
            {
                //Connect
                State = ConnectionState.Connecting;

                try
                {
                    IAsyncResult result = socket.BeginConnect(RemoteEndPoint, null, null);

                    result.AsyncWaitHandle.WaitOne(timeout);

                    socket.EndConnect(result);
                }
                catch (Exception e)
                {
                    throw new HazelException("Could not connect as an exception occured.", e);
                }

                //Start receiving data
                try
                {
                    StartWaitingForHeader(BodyReadCallback);
                }
                catch (Exception e)
                {
                    throw new HazelException("An exception occured while initiating the first receive operation.", e);
                }

                //Send handshake
                byte[] actualBytes;
                if (bytes == null)
                {
                    actualBytes = new byte[1];
                }
                else
                {
                    actualBytes = new byte[bytes.Length + 1];
                    Buffer.BlockCopy(bytes, 0, actualBytes, 1, bytes.Length);
                }
                
                //Set connected
                State = ConnectionState.Connected;

                SendBytes(actualBytes);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        ///     <include file="DocInclude/common.xml" path="docs/item[@name='Connection_SendBytes_General']/*" />
        ///     <para>
        ///         The sendOption parameter is ignored by the TcpConnection as TCP only supports FragmentedReliable 
        ///         communication, specifying anything else will have no effect.
        ///     </para>
        /// </remarks>
        public override void SendBytes(byte[] bytes, SendOption sendOption = SendOption.FragmentedReliable)
        {
            //Get bytes for length
            byte[] fullBytes = AppendLengthHeader(bytes);

            //Write the bytes to the socket
            lock (socketLock)
            {
                if (State != ConnectionState.Connected)
                    throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");
            
                try
                {
                    socket.BeginSend(fullBytes, 0, fullBytes.Length, SocketFlags.None, null, null);
                }
                catch (Exception e)
                {
                    HazelException he = new HazelException("Could not send data as an occured.", e);
                    HandleDisconnect(he);
                    throw he;
                }
            }

            Statistics.LogFragmentedSend(bytes.Length, fullBytes.Length);
        }

        /// <summary>
        ///     Called when a 4 byte header has been received.
        /// </summary>
        /// <param name="bytes">The 4 header bytes read.</param>
        /// <param name="callback">The callback to invoke when the body has been received.</param>
        void HeaderReadCallback(byte[] bytes, Action<byte[]> callback)
        {
            //Get length 
            int length = GetLengthFromBytes(bytes);

            //Begin receiving the body
            try
            {
                StartWaitingForBytes(length, callback);
            }
            catch (Exception e)
            {
                HandleDisconnect(new HazelException("An exception occured while initiating a body receive operation.", e));
            }
        }

        /// <summary>
        ///     Callback for when a body has been read.
        /// </summary>
        /// <param name="bytes">The data bytes received by the connection.</param>
        void BodyReadCallback(byte[] bytes)
        {
            //Begin receiving from the start
            try
            {
                StartWaitingForHeader(BodyReadCallback);
            }
            catch (Exception e)
            {
                HandleDisconnect(new HazelException("An exception occured while initiating a header receive operation.", e));
            }

            Statistics.LogFragmentedReceive(bytes.Length, bytes.Length + 4);

            //Fire DataReceived event
            InvokeDataReceived(bytes, SendOption.FragmentedReliable);
        }

        /// <summary>
        ///     Starts this connection receiving data.
        /// </summary>
        internal void StartReceiving()
        {
            try
            {
                StartWaitingForHeader(BodyReadCallback);
            }
            catch (Exception e)
            {
                HandleDisconnect(new HazelException("An exception occured while initiating the first receive operation.", e));
            }
        }

        /// <summary>
        ///     Starts waiting for a first handshake packet to be received.
        /// </summary>
        /// <param name="callback">The callback to invoke when the handshake has been received.</param>
        internal void StartWaitingForHandshake(Action<byte[]> callback)
        {
            try
            {
                StartWaitingForHeader(
                    delegate (byte[] bytes)
                    {
                        //Remove version byte
                        byte[] dataBytes = new byte[bytes.Length - 1];
                        Buffer.BlockCopy(bytes, 1, dataBytes, 0, bytes.Length - 1);

                        callback.Invoke(dataBytes);
                    }
                );
            }
            catch (Exception e)
            {
                HandleDisconnect(new HazelException("An exception occured while initiating the first receive operation.", e));
            }
        }

        /// <summary>
        ///     Starts this connections waiting for the header.
        /// </summary>
        /// <param name="callback">The callback to invoke when the body has been read.</param>
        void StartWaitingForHeader(Action<byte[]> callback)
        {
            StartWaitingForBytes(4, (bytes) => HeaderReadCallback(bytes, callback));
        }

        /// <summary>
        ///     Waits for the specified amount of bytes to be received.
        /// </summary>
        /// <param name="length">The number of bytes to receive.</param>
        /// <param name="callback">The callback </param>
        void StartWaitingForBytes(int length, Action<byte[]> callback)
        {
            StateObject state = new StateObject(length, callback);

            StartWaitingForChunk(state);
        }

        /// <summary>
        ///     Waits for the next chunk of data from this socket.
        /// </summary>
        /// <param name="state">The StateObject for the receive operation.</param>
        void StartWaitingForChunk(StateObject state)
        {
            lock (socketLock)
            {
                //Double check we've not disconnected then begin receiving
                if (State == ConnectionState.Connected || State == ConnectionState.Connecting)
                    socket.BeginReceive(state.buffer, state.totalBytesReceived, state.buffer.Length - state.totalBytesReceived, SocketFlags.None, ChunkReadCallback, state);
            }
        }

        /// <summary>
        ///     Called when a chunk has been read.
        /// </summary>
        /// <param name="result"></param>
        void ChunkReadCallback(IAsyncResult result)
        {
            int bytesReceived;

            //End the receive operation
            try
            {
                lock (socketLock)
                    bytesReceived = socket.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }
            catch (Exception e)
            {
                HandleDisconnect(new HazelException("An exception occured while completing a chunk read operation.", e));
                return;
            }

            StateObject state = (StateObject)result.AsyncState;

            state.totalBytesReceived += bytesReceived;      //TODO threading issues on state?

            //Exit if receive nothing
            if (bytesReceived == 0)
            {
                HandleDisconnect();
                return;
            }

            //If we need to receive more then wait for more, else process it.
            if (state.totalBytesReceived < state.buffer.Length)
            {
                try
                {
                    StartWaitingForChunk(state);
                }
                catch (Exception e)
                {
                    HandleDisconnect(new HazelException("An exception occured while initiating a chunk receive operation.", e));
                    return;
                }
            }
            else
                state.callback.Invoke(state.buffer);
        }

        /// <summary>
        ///     Called when the socket has been disconnected at the remote host.
        /// </summary>
        /// <param name="e">The exception if one was the cause.</param>
        void HandleDisconnect(HazelException e = null)
        {
            bool invoke = false;

            lock (socketLock)
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
                InvokeDisconnected(e);

                Dispose();
            }
        }

        /// <summary>
        ///     Appends the length header to the bytes.
        /// </summary>
        /// <param name="bytes">The source bytes.</param>
        /// <returns>The new bytes.</returns>
        static byte[] AppendLengthHeader(byte[] bytes)
        {
            byte[] fullBytes = new byte[bytes.Length + 4];

            //Append length
            fullBytes[0] = (byte)(((uint)bytes.Length >> 24) & 0xFF);
            fullBytes[1] = (byte)(((uint)bytes.Length >> 16) & 0xFF);
            fullBytes[2] = (byte)(((uint)bytes.Length >> 8) & 0xFF);
            fullBytes[3] = (byte)(uint)bytes.Length;

            //Add rest of bytes
            Buffer.BlockCopy(bytes, 0, fullBytes, 4, bytes.Length);

            return fullBytes;
        }

        /// <summary>
        ///     Returns the length from a length header.
        /// </summary>
        /// <param name="bytes">The bytes received.</param>
        /// <returns>The number of bytes.</returns>
        static int GetLengthFromBytes(byte[] bytes)
        {
            if (bytes.Length < 4)
                throw new IndexOutOfRangeException("Not enough bytes passed to calculate length.");

            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (socketLock)
                {
                    State = ConnectionState.NotConnected;

                    if (socket.Connected)
                        socket.Shutdown(SocketShutdown.Send);
                    socket.Close();
                }
            }

            base.Dispose(disposing);
        }
    }
}
