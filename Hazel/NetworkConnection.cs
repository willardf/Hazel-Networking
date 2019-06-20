using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;


namespace Hazel
{
    /// <summary>
    ///     Abstract base class for a <see cref="Connection"/> to a remote end point via a network protocol like TCP or UDP.
    /// </summary>
    /// <threadsafety static="true" instance="true"/>
    public abstract class NetworkConnection : Connection
    {
        /// <summary>
        ///     The remote end point of this connection.
        /// </summary>
        /// <remarks>
        ///     This is the end point of the other device given as an <see cref="System.Net.EndPoint"/> rather than a generic
        ///     <see cref="ConnectionEndPoint"/> as the base <see cref="Connection"/> does.
        /// </remarks>
        public EndPoint RemoteEndPoint { get; protected set; }

        public long GetIP4Address()
        {
            if (IPMode == IPMode.IPv4)
            {
                return ((IPEndPoint)this.RemoteEndPoint).Address.Address;
            }
            else
            {
                var bytes = ((IPEndPoint)this.RemoteEndPoint).Address.GetAddressBytes();
                return BitConverter.ToInt64(bytes, bytes.Length - 8);
            }
        }

        /// <summary>
        ///     Sends a disconnect message to the end point.
        /// </summary>
        protected abstract bool SendDisconnect(MessageWriter writer);

        /// <summary>
        ///     Called when the socket has been disconnected at the remote host.
        /// </summary>
        protected void DisconnectRemote(string reason, MessageReader reader)
        {
            if (this.SendDisconnect(null))
            {
                try
                {
                    InvokeDisconnected(reason, reader);
                }
                catch { }
            }

            this.Dispose();
        }

        /// <summary>
        ///     Called when the socket has been disconnected locally.
        /// </summary>
        public override void Disconnect(string reason, MessageWriter writer = null, bool fireEvent = true)
        {
            if (this.SendDisconnect(writer) && fireEvent)
            {
                try
                {
                    InvokeDisconnected(reason, null);
                }
                catch { }
            }

            this.Dispose();
        }
    }
}
