using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Hazel
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
        ///     Lock object for the writing to the state of the connection.
        /// </summary>
        Object stateLock = new Object();

        /// <summary>
        ///     Creates a UdpConnection for the virtual connection to the endpoint.
        /// </summary>
        /// <param name="listener">The listener that created this connection.</param>
        /// <param name="endPoint">The endpoint that we are connected to.</param>
        internal UdpServerConnection(UdpConnectionListener listener, EndPoint endPoint)
            : base()
        {
            this.Listener = listener;
            this.RemoteEndPoint = endPoint;
            this.EndPoint = new NetworkEndPoint(endPoint);

            State = ConnectionState.Connected;
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnection(byte[] bytes)
        {
            lock (stateLock)
            {
                if (State != ConnectionState.Connected)
                    throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");

                Listener.SendData(bytes, RemoteEndPoint);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        ///     This will always throw an InvalidOperationException.
        /// </remarks>
        public override void Connect(ConnectionEndPoint remoteEndPoint)
        {
            throw new InvalidOperationException("Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
        }

        /// <summary>
        ///     Called by the listener when we have data.
        /// </summary>
        /// <param name="buffer"></param>
        internal void InvokeDataReceived(byte[] buffer)
        {
           byte[] data = HandleReceive(buffer, buffer.Length);

           if (data != null)
                InvokeDataReceived(data, (SendOption)buffer[0]);
        }

        /// <inheritdoc />
        protected override void HandleDisconnect(HazelException e = null)
        {
            bool invoke = false;

            lock (stateLock)
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

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            //Here we just need to inform the listener we no longer need data.
            if (disposing)
            {
                lock (stateLock)
                {
                    Listener.RemoveConnectionTo(RemoteEndPoint);

                    State = ConnectionState.NotConnected;
                }
            }

            base.Dispose(disposing);
        }
    }
}
