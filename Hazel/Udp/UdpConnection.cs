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
    ///     Represents a connection that uses the UDP protocol.
    /// </summary>
    /// <inheritdoc />
    public abstract partial class UdpConnection : NetworkConnection
    {
        /// <summary>
        ///     Creates a new UdpConnection and initializes the keep alive timer.
        /// </summary>
        protected UdpConnection()
        {
            InitializeKeepAliveTimer();
        }

        /// <summary>
        ///     Writes the given bytes to the connection.
        /// </summary>
        /// <param name="bytes">The bytes to write.</param>
        protected abstract void WriteBytesToConnection(byte[] bytes);

        /// <inheritdoc/>
        /// <remarks>
        ///     <include file="DocInclude/common.xml" path="docs/item[@name='Connection_SendBytes_General']/*" />
        ///     <para>
        ///         Udp connections can currently send messages using <see cref="SendOption.None"/> and
        ///         <see cref="SendOption.Reliable"/>. Fragmented messages are not currently supported and will default to
        ///         <see cref="SendOption.None"/> until implemented.
        ///     </para>
        /// </remarks>
        public override void SendBytes(byte[] bytes, SendOption sendOption = SendOption.None)
        {
            //Early check
            if (State != ConnectionState.Connected)
                throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");

            //Add header information and send
            HandleSend(bytes, (byte)sendOption);
        }

        /// <summary>
        ///     Handles the reliable/fragmented sending from this connection.
        /// </summary>
        /// <param name="data">The data being sent.</param>
        /// <param name="sendOption">The <see cref="SendOption"/> specified as its byte value.</param>
        /// <param name="ackCallback">The callback to invoke when this packet is acknowledged.</param>
        /// <returns>The bytes that should actually be sent.</returns>
        protected void HandleSend(byte[] data, byte sendOption, Action ackCallback = null)
        {
            byte[] bytes;
            switch (sendOption)
            {
                //Handle reliable header and hellos
                case (byte)SendOption.Reliable:
                case (byte)SendOptionInternal.Hello:
                    bytes = new byte[data.Length + 3];
                    WriteReliableSendHeader(bytes, ackCallback);
                    break;

                default:
                    bytes = new byte[data.Length + 1];
                    break;
            }

            //Add message type
            bytes[0] = sendOption;

            //Copy data into new array
            Buffer.BlockCopy(data, 0, bytes, bytes.Length - data.Length, data.Length);

            //Inform keepalive not to send for a while
            ResetKeepAliveTimer();

            //Write to connection
            WriteBytesToConnection(bytes);
            
            Statistics.LogSend(data.Length, bytes.Length);
        }

        /// <summary>
        ///     Handles the receiving of data.
        /// </summary>
        /// <param name="buffer">The buffer containing the bytes received.</param>
        /// <param name="bytesReceived">The number of bytes that were received.</param>
        /// <returns>The bytes of data received.</returns>
        protected byte[] HandleReceive(byte[] buffer, int bytesReceived)
        {
            //Inform keepalive not to send for a while
            ResetKeepAliveTimer();

            int headerSize = 1;
            switch (buffer[0])
            {
                    //Handle reliable receives
                case (byte)SendOption.Reliable:
                    headerSize = 3;

                    if (HandleReliableReceive(buffer) == false)
                        return null;
                    break;

                    //Handle acknowledgments
                case (byte)SendOptionInternal.Acknowledgement:
                    HandleAcknowledgement(buffer);

                    return null;

                //We need to acknowledge hello messages so just use the same reliable receive
                //method
                case (byte)SendOptionInternal.Hello:
                    HandleReliableReceive(buffer);

                    return null;

                case (byte)SendOptionInternal.Disconnect:
                    HandleDisconnect();

                    return null;
            }

            byte[] dataBytes = new byte[bytesReceived - headerSize];
            Buffer.BlockCopy(buffer, headerSize, dataBytes, 0, dataBytes.Length);

            Statistics.LogReceive(dataBytes.Length, bytesReceived);

            return dataBytes;
        }

        /// <summary>
        ///     Sends a hello packet to the remote endpoint.
        /// </summary>
        /// <param name="acknowledgeCallback">The callback to invoke when the hello packet is acknowledged.</param>
        protected void SendHello(byte[] bytes, Action acknowledgeCallback)
        {
            //First byte of handshake is version indicator so add data after
            byte[] actualBytes;
            if (bytes == null)
            {
                actualBytes = new byte[1];
            }
            else
            {
                actualBytes = new byte[bytes.Length + 1];
                Buffer.BlockCopy(bytes, 0, actualBytes, 1, bytes.Length);
            }

            HandleSend(actualBytes, (byte)SendOptionInternal.Hello, acknowledgeCallback);
        }

        /// <summary>
        ///     Called when the socket has been disconnected at the remote host.
        /// </summary>
        /// <param name="e">The exception if one was the cause.</param>
        protected abstract void HandleDisconnect(HazelException e = null);

        /// <summary>
        ///     Sends a disconnect message to the end point.
        /// </summary>
        protected void SendDisconnect()
        {
            HandleSend(new byte[0], (byte)SendOptionInternal.Disconnect);       //TODO Should disconnect wait for an ack?
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeKeepAliveTimer();
            }

            base.Dispose(disposing);
        }
    }
}
