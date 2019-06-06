using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        public const int BufferSize = ushort.MaxValue;

        public int MinConnectionLength = 0;

        /// <summary>
        ///     The socket listening for connections.
        /// </summary>
        Socket socket;

        private Action<string> Logger;

        Timer reliablePacketTimer;

        /// <summary>
        ///     The connections we currently hold
        /// </summary>
        ConcurrentDictionary<EndPoint, UdpServerConnection> allConnections = new ConcurrentDictionary<EndPoint, UdpServerConnection>();
        
        public int ConnectionCount { get { return this.allConnections.Count; } }

        /// <summary>
        ///     Creates a new UdpConnectionListener for the given <see cref="IPAddress"/>, port and <see cref="IPMode"/>.
        /// </summary>
        /// <param name="endPoint">The endpoint to listen on.</param>
        public UdpConnectionListener(IPEndPoint endPoint, IPMode ipMode = IPMode.IPv4, Action<string> logger = null)
        {
            this.Logger = logger;
            this.EndPoint = endPoint;
            this.IPMode = ipMode;

            if (this.IPMode == IPMode.IPv4)
                this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new HazelException("IPV6 not supported!");

                this.socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                this.socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            }

            socket.ReceiveBufferSize = BufferSize;
            socket.SendBufferSize = BufferSize;
            
            reliablePacketTimer = new Timer(ManageReliablePackets, null, 100, Timeout.Infinite);
        }

        ~UdpConnectionListener()
        {
            this.Dispose(false);
        }
        
        private void ManageReliablePackets(object state)
        {
            foreach (var kvp in this.allConnections)
            {
                var sock = kvp.Value;
                sock.ManageReliablePackets();
            }

            try
            {
                this.reliablePacketTimer.Change(100, Timeout.Infinite);
            }
            catch { }
        }

        /// <inheritdoc />
        public override void Start()
        {
            try
            {
                socket.Bind(EndPoint);
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
        private void StartListeningForData()
        {
            EndPoint remoteEP = EndPoint;

            MessageReader message = null;
            try
            {
                message = MessageReader.GetSized(BufferSize);

                socket.BeginReceiveFrom(message.Buffer, 0, message.Buffer.Length, SocketFlags.None, ref remoteEP, ReadCallback, message);
                Interlocked.Increment(ref ActiveListeners);
            }
            catch (SocketException)
            {
                message?.Recycle();
                StartListeningForData();
                return;
            }
            catch (Exception ex)
            {
                //If the socket's been disposed then we can just end there.
                message.Recycle();
                this.Logger?.Invoke("Stopped due to: " + ex.Message);
                return;
            }
        }
        
        /// <summary>
        ///     Called when data has been received by the listener.
        /// </summary>
        /// <param name="result">The asyncronous operation's result.</param>
        
        public int ActiveListeners;
        public int PacketsReceived;

        void ReadCallback(IAsyncResult result)
        {
            Interlocked.Decrement(ref ActiveListeners);
            Interlocked.Increment(ref PacketsReceived);

            var message = (MessageReader)result.AsyncState;
            int bytesReceived;
            EndPoint remoteEndPoint = new IPEndPoint(IPMode == IPMode.IPv4 ? IPAddress.Any : IPAddress.IPv6Any, 0);

            //End the receive operation
            try
            {
                bytesReceived = socket.EndReceiveFrom(result, ref remoteEndPoint);

                message.Offset = 0;
                message.Length = bytesReceived;
            }
            catch (SocketException)
            {
                // Client no longer reachable, pretend it didn't happen
                // TODO should this not inform the connection this client is lost???

                // This thread suggests the IP is not passed out from WinSoc so maybe not possible
                // http://stackoverflow.com/questions/2576926/python-socket-error-on-udp-data-receive-10054
                message.Recycle();
                
                StartListeningForData();
                return;
            }
            catch (Exception ex)
            {
                //If the socket's been disposed then we can just end there.
                message.Recycle();
                this.Logger?.Invoke("Stopped due to: " + ex.Message);
                return;
            }

            // I'm a little concerned about a infinite loop here, but it seems like it's possible 
            // to get 0 bytes read on UDP without the socket being shut down.
            if (bytesReceived == 0)
            {
                message.Recycle();
                StartListeningForData();
                return;
            }

            //Begin receiving again
            StartListeningForData();

            bool aware = true;
            bool hasHelloByte = message.Buffer[0] == (byte)UdpSendOption.Hello;
            bool isHello = hasHelloByte && message.Length >= MinConnectionLength;

            //If we're aware of this connection use the one already
            //If this is a new client then connect with them!
            UdpServerConnection connection;
            if (!this.allConnections.TryGetValue(remoteEndPoint, out connection))
            {
                //Check for malformed connection attempts
                if (!isHello)
                {
                    message.Recycle();
                    return;
                }

                lock (this.allConnections)
                {
                    aware = this.allConnections.TryGetValue(remoteEndPoint, out connection);
                    if (!aware)
                    {
                        connection = new UdpServerConnection(this, (IPEndPoint)remoteEndPoint, this.IPMode);
                        if (!this.allConnections.TryAdd(remoteEndPoint, connection))
                        {
                            throw new Exception();
                        }
                    }
                }
            }

            //Inform the connection of the buffer (new connections need to send an ack back to client)
            connection.HandleReceive(message, bytesReceived);
            
            //If it's a new connection invoke the NewConnection event.
            if (!aware)
            {
                // Skip header and hello byte;
                message.Offset = 4;
                message.Length = bytesReceived - 4;
                message.Position = 0;
                InvokeNewConnection(message, connection);
            }
            else if (isHello || (!isHello && hasHelloByte))
            {
                message.Recycle();
            }
        }

#if DEBUG
        public int TestDropRate = -1;
        private int dropCounter = 0;
#endif

        /// <summary>
        ///     Sends data from the listener socket.
        /// </summary>
        /// <param name="bytes">The bytes to send.</param>
        /// <param name="endPoint">The endpoint to send to.</param>
        internal void SendData(byte[] bytes, int length, EndPoint endPoint)
        {
            if (length > bytes.Length) return;

#if DEBUG
            if (TestDropRate > 0)
            {
                if (Interlocked.Increment(ref dropCounter) % TestDropRate == 0)
                {
                    return;
                }
            }
#endif

            try
            {
                socket.BeginSendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    endPoint,
                    delegate (IAsyncResult result)
                    {
                        try
                        {
                            socket.EndSendTo(result);
                        }
                        catch { }
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
                socket.SendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    endPoint
                );
            }
            catch { }
        }

        /// <summary>
        ///     Removes a virtual connection from the list.
        /// </summary>
        /// <param name="endPoint">The endpoint of the virtual connection.</param>
        internal void RemoveConnectionTo(EndPoint endPoint)
        {
            this.allConnections.TryRemove(endPoint, out var conn);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            foreach (var kvp in this.allConnections)
            {
                kvp.Value.Dispose();
            }

            if (this.socket != null)
            {
                try { this.socket.Shutdown(SocketShutdown.Both); } catch { }
                try { this.socket.Close(); } catch { }
                try { this.socket.Dispose(); } catch { }
                this.socket = null;
            }

            this.reliablePacketTimer.Dispose();

            base.Dispose(disposing);
        }
    }
}
