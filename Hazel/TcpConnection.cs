using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

/* 
* Copyright (C) Jamie Read - All Rights Reserved
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential
* Written by Jamie Read <jamie.read@outlook.com>, January 2016
*/

namespace Hazel
{
    /// <summary>
    ///     Represents a connection that uses the TCP protocol.
    /// </summary>
    public class TcpConnection : Connection
    {
        /// <summary>
        ///     The socket we're managing.
        /// </summary>
        public Socket Socket { get; private set; }

        /// <summary>
        ///     The remote end point of this connection.
        /// </summary>
        public EndPoint RemoteEndPoint { get; protected set; }

        /// <summary>
        ///     Creates a TcpConnection from a given TCP Socket.
        /// </summary>
        /// <param name="socket"></param>
        internal TcpConnection(Socket socket)
        {
            //Check it's a TCP socket
            if (socket.ProtocolType != System.Net.Sockets.ProtocolType.Tcp)
                throw new ArgumentException("A TcpConnection requires a TCP socket.");

            this.EndPoint = new NetworkEndPoint(socket.RemoteEndPoint);
            this.RemoteEndPoint = socket.RemoteEndPoint;

            this.Socket = socket;

            lock (this.Socket)
            {
                this.Socket.NoDelay = true;
            }

            State = ConnectionState.Connected;
        }

        /// <summary>
        ///     Creates a new TCP connection.
        /// </summary>
        public TcpConnection()
        {
            //Create and connect a socket
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

            Socket.NoDelay = true;
        }

        /// <summary>
        ///     Internal call to start listening once this socket has been constructed and is ready.
        /// </summary>
        internal void StartListening()
        {
            //Start receiving data
            try
            {
                StartWaitingForHeader();
            }
            catch (SocketException e)
            {
                throw new HazelException("A Socket exception occured while initiating a receive operation.", e);
            }
        }

        /// <summary>
        ///     Connects this TCP connection to the endpoint.
        /// </summary>
        /// <param name="remotEndPoint">The location of the server to connect to.</param>
        public override void Connect(ConnectionEndPoint remoteEndPoint)
        {
            NetworkEndPoint nep = remoteEndPoint as NetworkEndPoint;
            if (nep == null)
            {
                throw new ArgumentException("The remote end point of a TCP connection must be a NetworkEndPoint.");
            }
            
            this.EndPoint = remoteEndPoint;
            this.RemoteEndPoint = nep.EndPoint;

            //Connect
            lock (Socket)
            {
                if (State != ConnectionState.NotConnected)
                    throw new InvalidOperationException("Cannot connect as the Connection is already connected.");

                State = ConnectionState.Connecting;

                try
                {
                    Socket.Connect(nep.EndPoint);
                }
                catch (SocketException e)
                {
                    throw new HazelException("Could not connect as a socket exception occured.", e);
                }
            }

            //Start receiving data
            try
            {
                StartWaitingForHeader();
            }
            catch (SocketException e)
            {
                throw new HazelException("A Socket exception occured while initiating a receive operation.", e);
            }

            //Set connected
            lock (Socket)
                State = ConnectionState.Connected;
        }

        /// <summary>
        ///     Writes an array of bytes to the connection and prefixes the length.
        /// </summary>
        /// <param name="bytes">The bytes of the message to send.</param>
        /// <param name="sendOption">The options this data is requested to send with.</param>
        /// <remarks>
        ///     The sendOptions parameter is ignored by the TcpConnection as TCP only supports OrderedFragmentedReliable communication.
        /// </remarks>
        public override void WriteBytes(byte[] bytes, SendOption sendOption = SendOption.OrderedFragmentedReliable)
        {
            //Get bytes for length
            byte[] fullBytes = Utility.AppendLengthHeader(bytes);

            //Write the bytes to the socket
            lock (Socket)
            {
                if (State != ConnectionState.Connected)
                    throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");
            
                try
                {
                    Socket.BeginSend(fullBytes, 0, fullBytes.Length, SocketFlags.None, null, null);
                }
                catch (SocketException e)
                {
                    HazelException he = new HazelException("Could not send data as a SocketException occured.", e);
                    HandleDisconnect(he);
                    throw he;
                }
            }

            Statistics.LogSend(bytes.Length, fullBytes.Length);
        }

        /// <summary>
        ///     Called when a 4 byte header has been received.
        /// </summary>
        /// <param name="result">The result of the async operation.</param>
        protected virtual void HeaderReadCallback(byte[] bytes)
        {
            //Get length 
            int length = Utility.GetLengthFromBytes(bytes);

            //Begin receiving the body
            try
            {
                StartWaitingForBytes(length, BodyReadCallback);
            }
            catch (SocketException e)
            {
                HandleDisconnect(new HazelException("A Socket exception occured while initiating a receive operation.", e));
            }
        }

        /// <summary>
        ///     Callback for when a body has been read.
        /// </summary>
        /// <param name="result"></param>
        protected virtual void BodyReadCallback(byte[] bytes)
        {
            //Begin receiving from the start
            StartWaitingForHeader();

            Statistics.LogReceive(bytes.Length, bytes.Length + 4);

            //Fire DataReceived event
            InvokeDataReceived(new DataEventArgs(bytes, SendOption.OrderedFragmentedReliable));
        }

        /// <summary>
        ///     Starts this connections waiting for the header.
        /// </summary>
        protected void StartWaitingForHeader()
        {
            StartWaitingForBytes(4, HeaderReadCallback);
        }

        /// <summary>
        ///     Waits for the specified amount of bytes to be received.
        /// </summary>
        /// <param name="length">The number of bytes to receive.</param>
        /// <param name="callback">The callback </param>
        protected virtual void StartWaitingForBytes(int length, Action<byte[]> callback)
        {
            StateObject state = new StateObject(length, callback);

            StartWaitingForChunk(state);
        }

        /// <summary>
        ///     Waits for the next chunk of data from this socket.
        /// </summary>
        /// <param name="state">The StateObject for the receive operation.</param>
        protected virtual void StartWaitingForChunk(StateObject state)
        {
            lock (Socket)
                Socket.BeginReceive(state.buffer, state.totalBytesReceived, state.buffer.Length, SocketFlags.None, ChunkReadCallback, state);
        }

        /// <summary>
        ///     Called when a chunk has been read.
        /// </summary>
        /// <param name="result"></param>
        protected virtual void ChunkReadCallback(IAsyncResult result)
        {
            int bytesReceived;

            //End the receive operation
            try
            {
                lock (Socket)
                    bytesReceived = Socket.EndReceive(result);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }

            StateObject state = (StateObject)result.AsyncState;

            state.totalBytesReceived += bytesReceived;

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
                catch (SocketException e)
                {
                    HandleDisconnect(new HazelException("A Socket exception occured while initiating a receive operation.", e));
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

            lock (Socket)
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
        ///     Closes this connections safely.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (Socket)
                {
                    State = ConnectionState.NotConnected;

                    if (Socket.Connected)
                        Socket.Shutdown(SocketShutdown.Send);
                    Socket.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
