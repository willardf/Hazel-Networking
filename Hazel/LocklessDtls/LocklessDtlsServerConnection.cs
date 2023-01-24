using Hazel.Udp;
using System;
using System.Collections.Concurrent;
using System.Net;

namespace Hazel.Dtls
{
    /// <summary>
    /// Represents a servers's connection to a client that uses the UDP protocol.
    /// WARNING! This is not fully tested in production!
    /// </summary>
    /// <inheritdoc/>
    public sealed class LocklessDtlsServerConnection : UdpConnection
    {
        public readonly DateTime CreationTime = DateTime.UtcNow;

        protected override bool DisposeOnDisconnect => false;

        /// <summary>
        ///     The connection listener that we use the socket of.
        /// </summary>
        /// <remarks>
        ///     Udp server connections utilize the same socket in the listener for sends/receives, this is the listener that 
        ///     created this connection and is hence the listener this conenction sends and receives via.
        /// </remarks>
        public LocklessDtlsConnectionListener Listener { get; private set; }

        internal PeerData PeerData { get; private set; }
        internal bool HelloProcessed { get; private set; }

        public readonly ConcurrentQueue<MessageReader> PacketsReceived = new ConcurrentQueue<MessageReader>();
        public readonly ConcurrentQueue<byte[]> PacketsSent = new ConcurrentQueue<byte[]>();
        
        /// <summary>
        ///     Creates a UdpConnection for the virtual connection to the endpoint.
        /// </summary>
        /// <param name="listener">The listener that created this connection.</param>
        /// <param name="endPoint">The endpoint that we are connected to.</param>
        /// <param name="IPMode">The IPMode we are connected using.</param>
        internal LocklessDtlsServerConnection(LocklessDtlsConnectionListener listener, IPEndPoint endPoint, IPMode IPMode, ILogger logger)
            : base(logger)
        {
            this.Listener = listener;
            this.EndPoint = endPoint;
            this.IPMode = IPMode;
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnection(byte[] bytes, int length)
        {
            if (bytes.Length != length) throw new ArgumentException("I made an assumption here. I hope you see this error.");

            if (this.State == ConnectionState.Disconnected)
            {
                return;
            }

            this.Listener.QueuePlaintextAppData(bytes, this);
        }

        /// <inheritdoc />
        /// <remarks>
        ///     This will always throw a HazelException.
        /// </remarks>
        public override void Connect(byte[] bytes = null, int timeout = 5000)
        {
            throw new InvalidOperationException("Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
        }

        /// <inheritdoc />
        /// <remarks>
        ///     This will always throw a HazelException.
        /// </remarks>
        public override void ConnectAsync(byte[] bytes = null)
        {
            throw new InvalidOperationException("Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
        }

        internal void SetPeerData(PeerData peerData)
        {
            if (this.PeerData != null)
            {
                throw new InvalidOperationException("Shouldn't be able to replace peer data");
            }

            this.PeerData = peerData;
            this.State = ConnectionState.Connecting;
        }

        internal void SetHelloAsProcessed()
        {
            this.State = ConnectionState.Connected;
            this.HelloProcessed = true;
            this.InitializeKeepAliveTimer();
        }

        /// <summary>
        ///     Sends a disconnect message to the end point.
        /// </summary>
        protected override bool SendDisconnect(MessageWriter data = null)
        {
            if (!Listener.RemoveConnectionTo(this.EndPoint)) return false;
            this._state = ConnectionState.Disconnected;

            var bytes = EmptyDisconnectBytes;
            if (data != null && data.Length > 0)
            {
                if (data.SendOption != SendOption.None) throw new ArgumentException("Disconnect messages can only be unreliable.");

                bytes = data.ToByteArray(true);
                bytes[0] = (byte)UdpSendOption.Disconnect;
            }

            this.Listener.QueuePlaintextAppData(bytes, this);

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            this.logger.WriteInfo("Disposed");
            this.Listener.RemoveConnectionTo(this.EndPoint);

            this.PeerData?.Dispose();
            base.Dispose(disposing);
        }
    }
}
