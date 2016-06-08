using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
        ///     Buffer to store incoming data in.
        /// </summary>
        byte[] dataBuffer = new byte[ushort.MaxValue];

        /// <summary>
        ///     The connections we currently hold
        /// </summary>
        Dictionary<EndPoint, UdpServerConnection> connections = new Dictionary<EndPoint, UdpServerConnection>();

        /// <summary>
        ///     Creates a new ConnectionListener for the given <see cref="IPAddress"/>, port and <see cref="IPMode"/>.
        /// </summary>
        /// <param name="IPAddress">The IPAddress to listen on.</param>
        /// <param name="port">The port to listen on.</param>
        /// <param name="mode">The <see cref="IPMode"/> to listen with.</param>
        public UdpConnectionListener(IPAddress IPAddress, int port, IPMode mode = IPMode.IPv4AndIPv6)
        {
            this.IPAddress = IPAddress;
            this.Port = port;

            if (mode == IPMode.IPv4)
                this.listener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            else
            {
                this.listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                this.listener.DualMode = true;
            }
        }

        /// <inheritdoc />
        public override void Start()
        {
            try
            {
                lock (listener)
                    listener.Bind(new IPEndPoint(IPAddress, Port));
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
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            
            try
            {
                lock (listener)
                    listener.BeginReceiveFrom(dataBuffer, 0, dataBuffer.Length, SocketFlags.None, ref remoteEP, ReadCallback, dataBuffer);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }

        /// <summary>
        ///     Called when data has been received by the listener.
        /// </summary>
        /// <param name="result">The asyncronous operation's result.</param>
        void ReadCallback(IAsyncResult result)
        {
            int bytesReceived;
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            //End the receive operation
            try
            {
                lock (listener)
                    bytesReceived = listener.EndReceiveFrom(result, ref remoteEndPoint);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }
            catch (SocketException e)
            {
                //Errrr... shit...
                //Not exactly much we can do if we've got here
                throw e;
            }

            //Exit if no bytes read, we've closed.
            if (bytesReceived == 0)
                return;

            //Copy to new buffer
            byte[] buffer = new byte[bytesReceived];
            Buffer.BlockCopy((byte[])result.AsyncState, 0, buffer, 0, bytesReceived);

            //Begin receiving again
            StartListeningForData();

            bool aware;
            UdpServerConnection connection;
            lock (connections)
            {
                aware = connections.ContainsKey(remoteEndPoint);

                //If we're aware of this connection use the one already
                if (aware)
                    connection = connections[remoteEndPoint];
                
                //If this is a new client then connect with them!
                else
                {
                    //Check for malformed connection attempts
                    if (buffer[0] != (byte)SendOptionInternal.Hello || buffer.Length != 3)
                        return;

                    connection = new UdpServerConnection(this, remoteEndPoint);
                    connections.Add(remoteEndPoint, connection);
                    
                    //Then ping back an ack to make sure they're happy
                    connection.SendAck(buffer[1], buffer[2]);
                }
            }

            //And fire the corresponding event
            if (aware)
                connection.InvokeDataReceived(buffer);
            else
                InvokeNewConnection(connection);
        }

        /// <summary>
        ///     Sends data from the listener socket.
        /// </summary>
        /// <param name="bytes">The bytes to send.</param>
        /// <param name="endPoint">The endpoint to send to.</param>
        internal void SendData(byte[] bytes, EndPoint endPoint)
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.SetBuffer(bytes, 0, bytes.Length);
            args.RemoteEndPoint = endPoint;

            try
            {
                lock (listener)
                    listener.SendToAsync(args);
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
            if (disposing)
            {
                lock (listener)
                    listener.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
