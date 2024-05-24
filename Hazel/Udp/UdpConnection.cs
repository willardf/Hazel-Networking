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
        protected readonly ObjectPool<SmartBuffer> bufferPool;

        public static readonly byte[] EmptyDisconnectBytes = new byte[] { (byte)UdpSendOption.Disconnect };

        public override float AveragePingMs => this._pingMs;
        protected readonly ILogger logger;


        public UdpConnection(ILogger logger) : base()
        {
            this.bufferPool = new ObjectPool<SmartBuffer>(() => new SmartBuffer(this.bufferPool, 1024));

            this.logger = logger;
            this.PacketPool = new ObjectPool<Packet>(() => new Packet(this));
        }

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
                socket.DontFragment = false;
            }
            catch { }

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
        protected abstract void WriteBytesToConnection(SmartBuffer bytes, int length);

        /// <inheritdoc/>
        public override SendErrors Send(MessageWriter msg)
        {
            if (this._state != ConnectionState.Connected)
            {
                return SendErrors.Disconnected;
            }

            using SmartBuffer buffer = this.bufferPool.GetObject();
            buffer.CopyFrom(msg);

            try
            {
                switch (msg.SendOption)
                {
                    case SendOption.Reliable:
                        ResetKeepAliveTimer();

                        AttachReliableID(buffer, 1, msg.Length);
                        WriteBytesToConnection(buffer, msg.Length);
                        Statistics.LogReliableSend(msg.Length - 3);
                        break;

                    default:
                        WriteBytesToConnection(buffer, msg.Length);
                        Statistics.LogUnreliableSend(msg.Length - 1);
                        break;
                }
            }
            catch (Exception e)
            {
                this.logger?.WriteError("Unknown exception while sending: " + e);
                return SendErrors.Unknown;
            }

            return SendErrors.None;
        }
        
        /// <summary>
        ///     Handles the reliable/fragmented sending from this connection.
        /// </summary>
        /// <param name="data">The data being sent.</param>
        /// <param name="sendOption">The <see cref="SendOption"/> specified as its byte value.</param>
        /// <param name="ackCallback">The callback to invoke when this packet is acknowledged.</param>
        /// <returns>The bytes that should actually be sent.</returns>
        protected virtual void HandleSend(byte[] data, byte sendOption, Action ackCallback = null)
        {
            switch (sendOption)
            {
                case (byte)UdpSendOption.Ping:
                case (byte)SendOption.Reliable:
                case (byte)UdpSendOption.Hello:
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
                    ProcessReliableReceive(message.Buffer, 1);
                    Statistics.LogPingReceive(bytesReceived);
                    message.Recycle();
                    break;

                case (byte)UdpSendOption.Hello:
                    ProcessReliableReceive(message.Buffer, 1);
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
        void UnreliableSend(byte sendOption, byte[] data)
        {
            this.UnreliableSend(sendOption, data, 0, data.Length);
        }

        /// <summary>
        ///     Sends bytes using the unreliable UDP protocol.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="sendOption">The SendOption to attach.</param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        void UnreliableSend(byte sendOption, byte[] data, int offset, int length)
        {
            using SmartBuffer buffer = this.bufferPool.GetObject();
            buffer.Length = length + 1;

            // Add message type and data
            buffer[0] = sendOption;
            Buffer.BlockCopy(data, offset, (byte[])buffer, buffer.Length - length, length);

            WriteBytesToConnection(buffer, buffer.Length);
            Statistics.LogUnreliableSend(length);
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
            Statistics.LogHelloSend();
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
