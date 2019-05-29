using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace Hazel.Udp
{
    /// <summary>
    ///     Represents a servers's connection to a client that uses the UDP protocol.
    /// </summary>
    /// <inheritdoc/>
    sealed class UdpServerConnection : UdpConnection
    {
        /// <summary>
        ///     The connection listener that we use the socket of.
        /// </summary>
        /// <remarks>
        ///     Udp server connections utilize the same socket in the listener for sends/receives, this is the listener that 
        ///     created this connection and is hence the listener this conenction sends and receives via.
        /// </remarks>
        public UdpConnectionListener Listener { get; private set; }
        
        /// <summary>
        ///     Creates a UdpConnection for the virtual connection to the endpoint.
        /// </summary>
        /// <param name="listener">The listener that created this connection.</param>
        /// <param name="endPoint">The endpoint that we are connected to.</param>
        /// <param name="IPMode">The IPMode we are connected using.</param>
        internal UdpServerConnection(UdpConnectionListener listener, IPEndPoint endPoint, IPMode IPMode)
            : base()
        {
            this.Listener = listener;
            this.RemoteEndPoint = endPoint;
            this.EndPoint = endPoint;
            this.IPMode = IPMode;

            State = ConnectionState.Connected;
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnection(byte[] bytes, int length)
        {
            Listener.SendData(bytes, length, RemoteEndPoint);
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
        public override void ConnectAsync(byte[] bytes = null, int timeout = 5000)
        {
            throw new InvalidOperationException("Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
        }


        /// <summary>
        ///     Sends a disconnect message to the end point.
        /// </summary>
        protected override void SendDisconnect()
        {
            try
            {
                Listener.SendDataSync(DisconnectBytes, 1, RemoteEndPoint);
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            Listener.RemoveConnectionTo(RemoteEndPoint);

            if (disposing)
            {
                if (this._state == ConnectionState.Connected
                    || this._state == ConnectionState.Disconnecting)
                {
                    SendDisconnect();
                    this._state = ConnectionState.NotConnected;
                }
            }

            
            base.Dispose(disposing);
        }
    }
}
