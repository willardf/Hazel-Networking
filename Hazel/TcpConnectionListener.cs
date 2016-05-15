using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

/* 
* Copyright (C) Jamie Read - All Rights Reserved
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential
* Written by Jamie Read <jamie.read@outlook.com>, January 2016
*/

namespace Hazel
{
    /// <summary>
    ///     Listens for new TCP connections and creates TCPConnections for them.
    /// </summary>
    public class TcpConnectionListener : NetworkConnectionListener
    {
        /// <summary>
        ///     The socket listening for connections.
        /// </summary>
        Socket listener;

        /// <summary>
        ///     Creates a new ConnectionListener for the given IP and port.
        /// </summary>
        /// <param name="ipAdress">The IPAddress to listen on.</param>
        /// <param name="port">The port to listen on.</param>
        public TcpConnectionListener(IPAddress IPAddress, int port)
        {
            this.IPAddress = IPAddress;
            this.Port = port;

            this.listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        ///     Makes this connection listener begin listening for connections.
        /// </summary>
        public override void Start()
        {
            try
            {
                lock (listener)
                {
                    listener.Bind(new IPEndPoint(IPAddress, Port));
                    listener.Listen(1000);

                    listener.BeginAccept(AcceptConnection, null);
                }
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
            lock (listener)
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
                listener.BeginAccept(new AsyncCallback(AcceptConnection), null);

                //Sort the event out
                TcpConnection tcpConnection = new TcpConnection(tcpSocket);

                NewConnectionEventArgs args = new NewConnectionEventArgs(tcpConnection);

                FireNewConnectionEvent(args);

                tcpConnection.StartListening();
            }
        }

        /// <summary>
        ///     Called when the object is being disposed.
        /// </summary>
        /// <param name="disposing">Are we being disposed?</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (listener)
                    listener.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
