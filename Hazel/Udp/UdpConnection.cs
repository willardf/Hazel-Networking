using System;
using System.Net.Sockets;

namespace Hazel.Udp
{
    /// <summary>
    ///     Represents a connection that uses the UDP protocol.
    /// </summary>
    /// <inheritdoc />
    public abstract partial class UdpConnection : NetworkConnection
    {
        public override float AveragePingMs => this._pingMs;

        private const int SioUdpConnectionReset = -1744830452;

        public static readonly byte[] EmptyDisconnectBytes = new byte[] { (byte)UdpSendOption.Disconnect };

        internal static Socket CreateSocket(IPMode ipMode)
        {
            Socket socket;
            if (ipMode == IPMode.IPv4)
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new InvalidOperationException("IPV6 not supported!");

                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            }

            try
            {
                socket.DontFragment = true;
            }
            catch
            {
                // ignored
            }

            try
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                socket.IOControl(SIO_UDP_CONNRESET, new byte[1], null);
            }
            catch { } // Only necessary on Windows

            return socket;
        }

        /// <summary>
        ///     Writes the given bytes to the connection.
        /// </summary>
        /// <param name="bytes">The bytes to write.</param>
        protected abstract void WriteBytesToConnection(byte[] bytes, int length, Action onTooBig = null);

        /// <inheritdoc/>
        public override void Send(MessageWriter msg)
        {
            if (this._state != ConnectionState.Connected)
                throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");
            
            if (msg.GetLength(false) >= Mtu)
            {
                FragmentedSend((byte)msg.SendOption, msg.ToByteArray(false));
                return;
            }

            byte[] buffer = msg.ToByteArray(true);

            switch (msg.SendOption)
            {
                case SendOption.Reliable:
                    ResetKeepAliveTimer();

                    AttachReliableID(buffer, 1);
                    WriteBytesToConnection(buffer, buffer.Length);
                    Statistics.LogReliableSend(buffer.Length - 3, buffer.Length);
                    break;

                default:
                    WriteBytesToConnection(buffer, buffer.Length);
                    Statistics.LogUnreliableSend(buffer.Length - 1, buffer.Length);
                    break;
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        ///     <include file="DocInclude/common.xml" path="docs/item[@name='Connection_SendBytes_General']/*" />
        ///     <para>
        ///         Udp connections can currently send messages using <see cref="SendOption.None"/> and
        ///         <see cref="SendOption.Reliable"/>. Fragmented messages are not currently supported and will default to
        ///         <see cref="SendOption.None"/> until implemented.
        ///     </para>
        /// </remarks>
        public override void SendBytes(byte[] bytes, SendOption sendOption = SendOption.None)
        {
            //Add header information and send
            HandleSend(bytes, (byte)sendOption);
        }
        
        /// <summary>
        ///     Handles the reliable/fragmented sending from this connection.
        /// </summary>
        /// <param name="data">The data being sent.</param>
        /// <param name="sendOption">The <see cref="SendOption"/> specified as its byte value.</param>
        /// <param name="ackCallback">The callback to invoke when this packet is acknowledged.</param>
        /// <returns>The bytes that should actually be sent.</returns>
        protected void HandleSend(byte[] data, byte sendOption, Action ackCallback = null)
        {
            switch (sendOption)
            {
                case (byte)UdpSendOption.Ping:
                case (byte)SendOption.Reliable:
                case (byte)UdpSendOption.Hello:
                case (byte)UdpSendOption.MtuTest:
                    ReliableSend(sendOption, data, ackCallback);
                    break;
                                    
                //Treat all else as unreliable
                default:
                    UnreliableSend(sendOption, data);
                    break;
            }
        }

        /// <summary>
        ///     Handles the receiving of data.
        /// </summary>
        /// <param name="message">The buffer containing the bytes received.</param>
        protected internal virtual void HandleReceive(MessageReader message, int bytesReceived)
        {
            ushort id;
            switch (message.Buffer[0])
            {
                //Handle reliable receives
                case (byte)SendOption.Reliable:
                    ReliableMessageReceive(message, bytesReceived);
                    break;

                //Handle acknowledgments
                case (byte)UdpSendOption.Acknowledgement:
                    AcknowledgementMessageReceive(message.Buffer, bytesReceived);
                    message.Recycle();
                    break;

                //We need to acknowledge hello and ping messages but dont want to invoke any events!
                case (byte)UdpSendOption.Ping:
                    ProcessReliableReceive(message.Buffer, 1, out id);
                    Statistics.LogHelloReceive(bytesReceived);
                    message.Recycle();
                    break;
                case (byte)UdpSendOption.Hello:
                    ProcessReliableReceive(message.Buffer, 1, out id);
                    Statistics.LogHelloReceive(bytesReceived);
                    message.Recycle();
                    break;

                case (byte)UdpSendOption.Disconnect:
                    message.Offset = 1;
                    message.Position = 0;
                    DisconnectRemote("The remote sent a disconnect request", message);
                    message.Recycle();
                    break;

                case (byte)SendOption.None:
                    InvokeDataReceived(SendOption.None, message, 1, bytesReceived);
                    Statistics.LogUnreliableReceive(bytesReceived - 1, bytesReceived);
                    break;

                case (byte)UdpSendOption.MtuTest:
                    MtuTestMessageReceive(message);
                    message.Recycle();
                    break;

                case (byte)UdpSendOption.Fragment:
                    FragmentMessageReceive(message);
                    message.Recycle();
                    break;

                // Treat everything else as garbage
                default:
                    message.Recycle();

                    // TODO: A new stat for unused data
                    Statistics.LogUnreliableReceive(bytesReceived - 1, bytesReceived);
                    break;
            }
        }

        /// <summary>
        ///     Sends bytes using the unreliable UDP protocol.
        /// </summary>
        /// <param name="sendOption">The SendOption to attach.</param>
        /// <param name="data">The data.</param>
        void UnreliableSend(byte sendOption, byte[] data, bool includeHeader = true)
        {
            var length = includeHeader ? data.Length + 1 : data.Length;
            if (length >= Mtu)
            {
                throw new HazelException("Unreliable messages can't be bigger than MTU");
            }

            var bytes = new byte[length];

            if (includeHeader)
            {
                bytes[0] = sendOption;
            }

            //Copy data into new array
            Buffer.BlockCopy(data, 0, bytes, length - data.Length, data.Length);

            //Write to connection
            WriteBytesToConnection(bytes, length);

            Statistics.LogUnreliableSend(data.Length, length);
        }

        /// <summary>
        ///     Helper method to invoke the data received event.
        /// </summary>
        /// <param name="sendOption">The send option the message was received with.</param>
        /// <param name="buffer">The buffer received.</param>
        /// <param name="dataOffset">The offset of data in the buffer.</param>
        void InvokeDataReceived(SendOption sendOption, MessageReader buffer, int dataOffset, int bytesReceived)
        {
            buffer.Offset = dataOffset;
            buffer.Length = bytesReceived - dataOffset;
            buffer.Position = 0;

            InvokeDataReceived(buffer, sendOption);
        }

        /// <summary>
        ///     Sends a hello packet to the remote endpoint.
        /// </summary>
        /// <param name="acknowledgeCallback">The callback to invoke when the hello packet is acknowledged.</param>
        protected void SendHello(byte[] bytes, Action acknowledgeCallback)
        {
            //First byte of handshake is version indicator so add data after
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

            HandleSend(actualBytes, (byte)UdpSendOption.Hello, acknowledgeCallback);
        }
                
        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeKeepAliveTimer();
                DisposeReliablePackets();
            }

            base.Dispose(disposing);
        }
    }
}
