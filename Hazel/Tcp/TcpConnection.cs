using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

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
        ///     Creates a TcpConnection from a given TCP Socket.
        /// </summary>
        /// <param name="socket">The TCP socket to wrap.</param>
        internal TcpConnection(Socket socket)
        {
            //Check it's a TCP socket
            if (socket.ProtocolType != ProtocolType.Tcp)
                throw new ArgumentException("A TcpConnection requires a TCP socket.");

            this.EndPoint = (IPEndPoint)socket.RemoteEndPoint;
            this.RemoteEndPoint = socket.RemoteEndPoint;

            this.socket = socket;
            this.socket.NoDelay = true;

            State = ConnectionState.Connected;
        }

        /// <summary>
        ///     Creates a new TCP connection.
        /// </summary>
        /// <param name="remoteEndPoint">A <see cref="NetworkEndPoint"/> to connect to.</param>
        public TcpConnection(IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4)
        {
            this.EndPoint = remoteEndPoint;
            this.RemoteEndPoint = remoteEndPoint;
            this.IPMode = ipMode;

            //Create a socket
            if (ipMode == IPMode.IPv4)
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new InvalidOperationException("IPV6 not supported!");

                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
            }

            socket.NoDelay = true;
        }

        /// <inheritdoc />
        public override void Connect(byte[] bytes = null, int timeout = 5000)
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
                var msg = MessageReader.GetSized(ushort.MaxValue);

                ListenForData(msg, InvokeAndListen);
            }
            catch (Exception e)
            {
                throw new HazelException("An exception occured while initiating the first receive operation.", e);
            }

            //Set connected
            State = ConnectionState.Connected;

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

            SendBytes(actualBytes);
        }

        public override void ConnectAsync(byte[] bytes = null, int timeout = 5000)
        {
            throw new NotImplementedException("I don't need this, so I didn't make it.");
        }

        public override void Send(MessageWriter msg)
        {
            if (msg.SendOption != SendOption.Tcp) throw new InvalidOperationException("Sorry, no can do, holmes.");

            if (State != ConnectionState.Connected)
                throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");

            var fullBytes = PrependLengthHeader(msg.Buffer, msg.Length);

            try
            {
                socket.BeginSend(fullBytes, 0, fullBytes.Length, SocketFlags.None, FinishSend, null);
            }
            catch (Exception e)
            {
                Disconnect("Could not send data as an occured: " + e.Message);
            }

            Statistics.LogFragmentedSend(msg.Length, fullBytes.Length);
        }

        /// <inheritdoc/>
        /// <remarks>
        ///     <include file="DocInclude/common.xml" path="docs/item[@name='Connection_SendBytes_General']/*" />
        ///     <para>
        ///         The sendOption parameter is ignored by the TcpConnection as TCP only supports FragmentedReliable 
        ///         communication, specifying anything else will have no effect.
        ///     </para>
        /// </remarks>
        public override void SendBytes(byte[] bytes, SendOption sendOption = SendOption.Tcp)
        {
            if (State != ConnectionState.Connected)
                throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");

            var fullBytes = PrependLengthHeader(bytes);

            try
            {
                socket.BeginSend(fullBytes, 0, fullBytes.Length, SocketFlags.None, FinishSend, null);
            }
            catch (Exception e)
            {
                Disconnect("Could not send data as an occured: " + e.Message);
            }

            Statistics.LogFragmentedSend(bytes.Length, fullBytes.Length);
        }

        private void FinishSend(IAsyncResult ar)
        {
            try
            {
                this.socket.EndSend(ar);
            }
            catch { }
        }

        /// <summary>
        ///     Starts waiting for a first handshake packet to be received.
        /// </summary>
        /// <param name="callback">The callback to invoke when the handshake has been received.</param>
        internal void StartWaitingForHandshake(Action<MessageReader> callback)
        {
            this.State = ConnectionState.Connected;

            var buffer = MessageReader.GetSized(ushort.MaxValue);
            try
            {
                buffer.Offset = 0;
                buffer.Length = 4;
                buffer.Position = 0;

                ListenForData(
                    buffer,
                    m => ReadHeader(m,
                        delegate (MessageReader msg)
                        {
                            ListenForData();

                            //Remove version byte
                            msg.Offset = 1;
                            msg.Length -= 1;
                            msg.Position = 0;

                            callback.Invoke(msg);
                        })
                );
            }
            catch (Exception e)
            {
                buffer.Recycle();
                Disconnect("An exception occured while initiating the first receive operation: " + e.Message);
            }
        }

        private void InvokeAndListen(MessageReader msg)
        {
            this.ListenForData();

            try
            {
                this.InvokeDataReceived(msg, SendOption.Tcp);
            }
            catch { }
        }
        
        private void ListenForData()
        {
            var msg = MessageReader.GetSized(ushort.MaxValue);
            msg.Offset = 0;
            msg.Length = 4;
            msg.Position = 0;

            ListenForData(msg, m => ReadHeader(m, null));
        }

        private void ReadHeader(MessageReader msg, Action<MessageReader> callback)
        {
            msg.Length = GetLengthFromBytes(msg.Buffer);
            msg.Position = 0;

            ListenForData(msg, callback ?? InvokeAndListen);
        }

        private void ListenForData(MessageReader msg, Action<MessageReader> callback)
        {
            if (State == ConnectionState.Disconnecting || State == ConnectionState.NotConnected)
                throw new HazelException("Not connected");
            
            try
            {
                socket.BeginReceive(msg.Buffer, msg.Position, msg.Length, SocketFlags.None, o => ReadUntilFull(callback, o), msg);
            }
            catch (SocketException s)
            {
                msg.Recycle();
                Disconnect("SocketException while reading header: " + s.Message);
            }
        }
        
        private void ReadUntilFull(Action<MessageReader> callback, IAsyncResult result)
        {
            int bytesRead;
            try
            {
                bytesRead = socket.EndReceive(result);
                if (bytesRead == 0)
                {
                    Disconnect("Received 0 bytes");
                    return;
                }
            }
            catch (ObjectDisposedException) { return; }
            catch (SocketException s)
            {
                Disconnect("SocketException while reading body: " + s.Message);
                return;
            }

            var msg = (MessageReader)result.AsyncState;
            msg.Position += bytesRead;

            Statistics.LogFragmentedReceive(bytesRead, 0);

            if (msg.Position < msg.Length)
            {
                ListenForData(msg, callback);
            }
            else
            {
                try
                {
                    msg.Position = 0;
                    callback(msg);
                }
                catch { }
            }
        }

        protected override void SendDisconnect()
        {
            // Just dispose the connection, it's inherent to TCP.
        }

        /// <summary>
        ///     Appends the length header to the bytes.
        /// </summary>
        /// <param name="bytes">The source bytes.</param>
        /// <returns>The new bytes.</returns>
        private static byte[] PrependLengthHeader(byte[] bytes, int length = -1)
        {
            length = length > -1 ? length : bytes.Length;

            byte[] fullBytes = new byte[length + 4];
            Buffer.BlockCopy(bytes, 0, fullBytes, 4, length);

            fullBytes[0] = (byte)(length >> 24);
            fullBytes[1] = (byte)(length >> 16);
            fullBytes[2] = (byte)(length >> 8);
            fullBytes[3] = (byte)length;

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
                lock (this)
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