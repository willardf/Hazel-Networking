using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Hazel.Udp
{
    /// <summary>
    ///     Listens for new UDP connections and creates UdpConnections for them.
    /// </summary>
    /// <inheritdoc />
    public class UdpConnectionListener : NetworkConnectionListener
    {
        /// <summary>
        ///     The socket listening for connections.
        /// </summary>
        Socket listener;
        
        /// <summary>
        ///     The connections we currently hold
        /// </summary>
        Dictionary<EndPoint, UdpServerConnection> connections = new Dictionary<EndPoint, UdpServerConnection>();

        /// <summary>
        ///     Creates a new UdpConnectionListener for the given <see cref="IPAddress"/>, port and <see cref="IPMode"/>.
        /// </summary>
        /// <param name="IPAddress">The IPAddress to listen on.</param>
        /// <param name="port">The port to listen on.</param>
        /// <param name="mode">The <see cref="IPMode"/> to listen with.</param>
        [Obsolete("Temporary constructor in beta only, use NetworkEndPoint constructor instead.")]
        public UdpConnectionListener(IPAddress IPAddress, int port, IPMode mode = IPMode.IPv4)
            : this (new NetworkEndPoint(IPAddress, port, mode))
        {

        }

        /// <summary>
        ///     Creates a new UdpConnectionListener for the given <see cref="IPAddress"/>, port and <see cref="IPMode"/>.
        /// </summary>
        /// <param name="endPoint">The endpoint to listen on.</param>
        public UdpConnectionListener(NetworkEndPoint endPoint)
        {
            this.EndPoint = endPoint.EndPoint;
            this.IPMode = endPoint.IPMode;

            if (endPoint.IPMode == IPMode.IPv4)
                this.listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new HazelException("IPV6 not supported!");

                this.listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                this.listener.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, false);
            }
        }

        ~UdpConnectionListener()
        {
            this.Dispose(false);
        }

        /// <inheritdoc />
        public override void Start()
        {
            try
            {
                listener.Bind(EndPoint);
            }
            catch (SocketException e)
            {
                throw new HazelException("Could not start listening as a SocketException occured", e);
            }

            StartListeningForData();
        }

        /// <summary>
        ///     Instructs the listener to begin listening.
        /// </summary>
        void StartListeningForData()
        {
            EndPoint remoteEP = EndPoint;
            
            try
            {
                var message = MessageReader.GetSized(ushort.MaxValue);
                listener.BeginReceiveFrom(message.Buffer, 0, message.Buffer.Length, SocketFlags.None, ref remoteEP, ReadCallback, message);
                Interlocked.Increment(ref ActiveThreads);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                //Client no longer reachable, pretend it didn't happen
                //TODO possibly able to disconnect client, see other TODO
                StartListeningForData();
                return;
            }
        }

        /// <summary>
        ///     Called when data has been received by the listener.
        /// </summary>
        /// <param name="result">The asyncronous operation's result.</param>

        private int ActiveThreads;
        void ReadCallback(IAsyncResult result)
        {
            int bytesReceived;
            EndPoint remoteEndPoint = new IPEndPoint(IPMode == IPMode.IPv4 ? IPAddress.Any : IPAddress.IPv6Any, 0);

            //End the receive operation
            try
            {
                Interlocked.Decrement(ref ActiveThreads);
                bytesReceived = listener.EndReceiveFrom(result, ref remoteEndPoint);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }
            catch (SocketException)
            {
                //Client no longer reachable, pretend it didn't happen
                //TODO should this not inform the connection this client is lost???

                //This thread suggests the IP is not passed out from WinSoc so maybe not possible
                //http://stackoverflow.com/questions/2576926/python-socket-error-on-udp-data-receive-10054
                StartListeningForData();
                return;
            }

            //Exit if no bytes read, we've closed.
            if (bytesReceived == 0)
                return;
            
            //Begin receiving again
            StartListeningForData();

            var message = (MessageReader)result.AsyncState;
            message.Length = bytesReceived;

            bool aware;
            UdpServerConnection connection;
            lock (connections)
            {
                //If we're aware of this connection use the one already
                //If this is a new client then connect with them!
                if (!(aware = connections.TryGetValue(remoteEndPoint, out connection)))
                {
                    //Check for malformed connection attempts
                    if (message.Buffer[0] != (byte)UdpSendOption.Hello)
                        return;

                    connection = new UdpServerConnection(this, remoteEndPoint, IPMode);
                    connections.Add(remoteEndPoint, connection);
                }
            }

            //Inform the connection of the buffer (new connections need to send an ack back to client)
            connection.HandleReceive(message);

            //If it's a new connection invoke the NewConnection event.
            if (!aware)
            {
                // Skip header and hello byte;
                message.Offset = 4; 
                message.Length = bytesReceived - 4;
                message.Position = 0;
                InvokeNewConnection(message, connection);
            }
        }

        /// <summary>
        ///     Sends data from the listener socket.
        /// </summary>
        /// <param name="bytes">The bytes to send.</param>
        /// <param name="endPoint">The endpoint to send to.</param>
        internal void SendData(byte[] bytes, int length, EndPoint endPoint)
        {
            if (length > bytes.Length) return;

            try
            {
                listener.BeginSendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    endPoint,
                    delegate (IAsyncResult result)
                    {
                        listener.EndSendTo(result);
                    },
                    null
                );
            }
            catch (SocketException e)
            {
                throw new HazelException("Could not send data as a SocketException occured.", e);
            }
            catch (ObjectDisposedException)
            {
                //Keep alive timer probably ran, ignore
                return;
            }
        }

        /// <summary>
        ///     Sends data from the listener socket.
        /// </summary>
        /// <param name="bytes">The bytes to send.</param>
        /// <param name="endPoint">The endpoint to send to.</param>
        internal void SendDataSync(byte[] bytes, int length, EndPoint endPoint)
        {
            try
            {
                listener.SendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    endPoint
                );
            }
            catch (SocketException e)
            {
                throw new HazelException("Could not send data as a SocketException occured.", e);
            }
            catch (ObjectDisposedException)
            {
                //Keep alive timer probably ran, ignore
                return;
            }
        }

        /// <summary>
        ///     Removes a virtual connection from the list.
        /// </summary>
        /// <param name="endPoint">The endpoint of the virtual connection.</param>
        internal void RemoveConnectionTo(EndPoint endPoint)
        {
            lock (connections)
                connections.Remove(endPoint);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            lock (connections)
            {
                var connects = this.connections.ToArray();
                foreach (var kvp in connects)
                {
                    if (kvp.Value.State == ConnectionState.Connected)
                    {
                        try
                        {
                            kvp.Value.SendDisconnect();
                        }
                        catch { }
                    }

                    kvp.Value.Dispose();
                }

                connections.Clear();
            }

            if (listener != null)
            {
                listener.Close();
                this.listener.Dispose();
                this.listener = null;
            }

            base.Dispose(disposing);
        }
    }
}
