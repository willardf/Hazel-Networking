using System;
using System.Net;
using System.Net.Sockets;

namespace Hazel.Tcp
{
    public sealed class TcpConnectionListener : NetworkConnectionListener
    {
        private Socket listener;

        /// <summary>
        ///     Creates a new TcpConnectionListener for the given <see cref="IPAddress"/>, port and <see cref="IPMode"/>.
        /// </summary>
        /// <param name="endPoint">The end point to listen on.</param>
        public TcpConnectionListener(IPEndPoint endPoint, IPMode ipMode = IPMode.IPv4)
        {
            this.EndPoint = endPoint;
            this.IPMode = ipMode;

            if (this.IPMode == IPMode.IPv4)
                this.listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new InvalidOperationException("IPV6 not supported!");

                this.listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                this.listener.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
            }
        }

        /// <inheritdoc />
        public override void Start()
        {
            try
            {
                listener.Bind(EndPoint);
                listener.Listen(1000);

                listener.BeginAccept(AcceptConnection, null);
            }
            catch (SocketException e)
            {
                throw new HazelException("Could not start listening as a SocketException occured", e);
            }
        }

        /// <summary>
        ///     Called when a new connection has been accepted by the listener.
        /// </summary>
        /// <param name="result">The asyncronous operation's result.</param>
        void AcceptConnection(IAsyncResult result)
        {
            //Accept Tcp socket
            Socket tcpSocket;
            try
            {
                tcpSocket = listener.EndAccept(result);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }

            //Start listening for the next connection
            listener.BeginAccept(AcceptConnection, null);

            //Sort the event out
            TcpConnection tcpConnection = new TcpConnection(tcpSocket);

            //Wait for handshake
            tcpConnection.StartWaitingForHandshake(
                delegate (MessageReader msg)
                {
                    InvokeNewConnection(msg, tcpConnection);
                }
            );
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                listener.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}