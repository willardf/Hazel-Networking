using System;
using System.Net;

namespace Hazel.Udp.FewerThreads
{
    /// <summary>
    ///     Represents a servers's connection to a client that uses the UDP protocol.
    /// </summary>
    /// <inheritdoc/>
    internal sealed class UdpServerConnection2 : UdpConnection
    {
        /// <summary>
        ///     The connection listener that we use the socket of.
        /// </summary>
        /// <remarks>
        ///     Udp server connections utilize the same socket in the listener for sends/receives, this is the listener that 
        ///     created this connection and is hence the listener this conenction sends and receives via.
        /// </remarks>
        public UdpConnectionListener2 Listener { get; private set; }

        /// <summary>
        ///     Creates a UdpConnection for the virtual connection to the endpoint.
        /// </summary>
        /// <param name="listener">The listener that created this connection.</param>
        /// <param name="endPoint">The endpoint that we are connected to.</param>
        /// <param name="IPMode">The IPMode we are connected using.</param>
        internal UdpServerConnection2(UdpConnectionListener2 listener, IPEndPoint endPoint, IPMode IPMode)
            : base()
        {
            this.Listener = listener;
            this.RemoteEndPoint = endPoint;
            this.EndPoint = endPoint;
            this.IPMode = IPMode;

            State = ConnectionState.Connected;
            this.InitializeKeepAliveTimer();
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnection(byte[] bytes, int length)
        {
            if (bytes.Length != length) throw new ArgumentException("I made an assumption here. I hope you see this error.");

            Listener.SendDataRaw(bytes, RemoteEndPoint);
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

        /// <summary>
        ///     Sends a disconnect message to the end point.
        /// </summary>
        protected override bool SendDisconnect(MessageWriter data = null)
        {
            if (!Listener.RemoveConnectionTo(RemoteEndPoint)) return false;
            this._state = ConnectionState.NotConnected;
            
            var bytes = EmptyDisconnectBytes;
            if (data != null && data.Length > 0)
            {
                if (data.SendOption != SendOption.None) throw new ArgumentException("Disconnect messages can only be unreliable.");

                bytes = data.ToByteArray(true);
                bytes[0] = (byte)UdpSendOption.Disconnect;
            }

            try
            {
                Listener.SendDataRaw(bytes, RemoteEndPoint);
            }
            catch { }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SendDisconnect();
            }

            base.Dispose(disposing);
        }
    }
}
