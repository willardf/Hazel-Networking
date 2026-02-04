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

        /// <summary>
        /// Whether this endpoint supports application-level fragmentation and MTU discovery.
        /// This is a local capability flag.
        /// </summary>
        public bool FragmentationSupported { get; }

        /// <summary>
        /// Whether application-level fragmentation and MTU discovery are enabled for this connection.
        /// This is the negotiated result and is only true when both endpoints support it.
        /// </summary>
        public bool FragmentationEnabled { get; private set; }

        /// <summary>
        /// The remote endpoint's hello version byte (capabilities) as observed from its hello.
        /// </summary>
        public HazelHelloVersion RemoteHelloVersion { get; private set; } = HazelHelloVersion.Legacy;

        public static readonly byte[] EmptyDisconnectBytes = new byte[] { (byte)UdpSendOption.Disconnect };

        public override float AveragePingMs => this._pingMs;
        protected readonly ILogger logger;

        protected virtual bool UseMtuDiscovery => true;


        public UdpConnection(ILogger logger, bool enableFragmentation = false) : base()
        {
            this.bufferPool = new ObjectPool<SmartBuffer>(() => new SmartBuffer(this.bufferPool, 1024));

            this.logger = logger;
            this.PacketPool = new ObjectPool<Packet>(() => new Packet(this));

            this.FragmentationSupported = enableFragmentation;

            // Negotiated later (via hello version exchange). Default to legacy behaviour.
            this.FragmentationEnabled = false;
        }

        internal void SetRemoteHelloVersion(byte ver)
        {
            RemoteHelloVersion = (HazelHelloVersion)ver;

            var wasEnabled = this.FragmentationEnabled;
            this.FragmentationEnabled =
                this.FragmentationSupported &&
                this.RemoteHelloVersion >= HazelHelloVersion.Fragmentation;

            if (wasEnabled != this.FragmentationEnabled)
            {
                this.ApplyDontFragment(this.FragmentationEnabled);
            }

            // Start MTU discovery as soon as fragmentation becomes enabled on a connected socket.
            if (!wasEnabled && this.FragmentationEnabled && this._state == ConnectionState.Connected)
            {
                this.StartMtuDiscovery();
            }
        }

        protected virtual void ApplyDontFragment(bool dontFragment)
        {
        }

        internal static Socket CreateSocket(IPMode ipMode, bool enableFragmentation = false)
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
                // Default to OS fragmentation. We only enable DontFragment after capability negotiation.
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
        protected abstract void WriteBytesToConnection(SmartBuffer bytes, int length, Action<SocketException> onError = null);

        /// <inheritdoc/>
        public override SendErrors Send(MessageWriter msg)
        {
            if (this._state != ConnectionState.Connected)
            {
                return SendErrors.Disconnected;
            }

            // Optional application-level fragmentation and MTU discovery.
            // If enabled, we can fragment large reliable messages. For unreliable messages
            // that exceed the MTU we only log a warning and attempt to send without killing
            // the connection.
            var isOversizeUnreliable = this.FragmentationEnabled
                && msg.SendOption != SendOption.Reliable
                && msg.Length > this.Mtu;

            if (this.FragmentationEnabled && msg.SendOption == SendOption.Reliable && msg.Length > this.Mtu)
            {
                ResetKeepAliveTimer();
                FragmentedSend((byte)SendOption.Reliable, msg.ToByteArray(false));
                return SendErrors.None;
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
                        if (isOversizeUnreliable)
                        {
                            this.logger?.WriteWarning($"Attempted to send unreliable message of size {msg.Length} which exceeds MTU {this.Mtu}. The packet may be dropped.");
                            // Provide an error handler so we don't disconnect on MessageSize errors.
                            WriteBytesToConnection(buffer, msg.Length, _ => { });
                        }
                        else
                        {
                            WriteBytesToConnection(buffer, msg.Length);
                        }
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
                    // Hello carries the remote capability byte as the first byte of its payload.
                    // Layout: [SendOption(1)][ReliableId(2)][HelloVersion(1)]...
                    if (bytesReceived >= 4)
                    {
                        this.SetRemoteHelloVersion(message.Buffer[3]);
                    }
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
                    // We can safely process MTU test messages as long as we support the feature locally.
                    // (The negotiated flag only gates what we send.)
                    if (this.FragmentationSupported)
                    {
                        MtuTestMessageReceive(message);
                        message.Recycle();
                    }
                    else
                    {
                        message.Recycle();
                        Statistics.LogUnreliableReceive(bytesReceived - 1, bytesReceived);
                    }
                    break;

                case (byte)UdpSendOption.Fragment:
                    // We can safely process fragments as long as we support the feature locally.
                    // (The negotiated flag only gates what we send.)
                    if (this.FragmentationSupported)
                    {
                        FragmentMessageReceive(message, bytesReceived);
                        message.Recycle();
                    }
                    else
                    {
                        message.Recycle();
                        Statistics.LogUnreliableReceive(bytesReceived - 1, bytesReceived);
                    }
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
            var isOversize = this.FragmentationEnabled && (length + 1 > this.Mtu);
            if (isOversize)
            {
                this.logger?.WriteWarning($"Attempted to send unreliable message of size {length + 1} which exceeds MTU {this.Mtu}. The packet may be dropped.");
            }

            using SmartBuffer buffer = this.bufferPool.GetObject();
            buffer.Length = length + 1;

            // Add message type and data
            buffer[0] = sendOption;
            Buffer.BlockCopy(data, offset, (byte[])buffer, buffer.Length - length, length);

            if (isOversize)
            {
                // Provide an error handler so we don't disconnect on MessageSize errors.
                WriteBytesToConnection(buffer, buffer.Length, _ => { });
            }
            else
            {
                WriteBytesToConnection(buffer, buffer.Length);
            }
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

            // Write our hello version/capability byte.
            actualBytes[0] = this.FragmentationSupported
                ? (byte)HazelHelloVersion.Fragmentation
                : (byte)HazelHelloVersion.Legacy;

            HandleSend(actualBytes, (byte)UdpSendOption.Hello, acknowledgeCallback);
        }

        /// <summary>
        /// Sends a hello response containing only this endpoint's hello version byte.
        /// This is used for capability negotiation so that the client can learn the server's capability.
        /// </summary>
        internal void SendHelloResponse()
        {
            // Empty payload (only the version byte is sent).
            SendHello(null, null);
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
