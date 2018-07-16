using System;
using System.Collections.Generic;
using System.IO;
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
        ///     Sends a number of bytes to the end point of the connection using the specified <see cref="SendOption"/>.
        /// </summary>
        /// <param name="bytes">The bytes of the message to send.</param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="sendOption">The option specifying how the message should be sent.</param>
        /// <remarks>
        ///     <include file="DocInclude/common.xml" path="docs/item[@name='Connection_SendBytes_General']/*" />
        ///     <para>
        ///         The sendOptions parameter is only a request to use those options and the actual method used to send the
        ///         data is up to the implementation. There are circumstances where this parameter may be ignored but in 
        ///         general any implementer should aim to always follow the user's request.
        ///     </para>
        /// </remarks>
        public override void SendBytes(byte[] bytes, int offset, int length, SendOption sendOption = SendOption.None)
        {
            //Early check
            if (State != ConnectionState.Connected)
                throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");


            //Inform keepalive not to send for a while
            ResetKeepAliveTimer();

            switch (sendOption)
            {
                //Handle reliable header and hellos
                case SendOption.Reliable:
                    ReliableSend((byte)sendOption, bytes, offset, length);
                    break;

                case SendOption.FragmentedReliable:
                    throw new NotImplementedException();
                    // FragmentedSend(data);
                    // break;

                //Treat all else as unreliable
                default:
                    UnreliableSend((byte)sendOption, bytes, offset, length);
                    break;
            }
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
            //Inform keepalive not to send for a while
            ResetKeepAliveTimer();

            switch (sendOption)
            {
                //Handle reliable header and hellos
                case (byte)SendOption.Reliable:
                case (byte)UdpSendOption.Hello:
                    ReliableSend(sendOption, data, ackCallback);
                    break;

                case (byte)SendOption.FragmentedReliable:
                    FragmentedSend(data);
                    break;
                
                //Treat all else as unreliable
                default:
                    UnreliableSend(sendOption, data);
                    break;
            }
        }

        /// <summary>
        ///     Handles the receiving of data.
        /// </summary>
        /// <param name="buffer">The buffer containing the bytes received.</param>
        protected internal void HandleReceive(byte[] buffer)
        {
            //Inform keepalive not to send for a while
            ResetKeepAliveTimer();
            
            switch (buffer[0])
            {
                //Handle reliable receives
                case (byte)SendOption.Reliable:
                    ReliableMessageReceive(buffer);
                    break;

                //Handle acknowledgments
                case (byte)UdpSendOption.Acknowledgement:
                    AcknowledgementMessageReceive(buffer);
                    break;

                //We need to acknowledge hello messages but dont want to invoke any events!
                case (byte)UdpSendOption.Hello:
                    ProcessReliableReceive(buffer, 1);
                    Statistics.LogHelloReceive(buffer.Length);
                    break;

                case (byte)UdpSendOption.Disconnect:
                    HandleDisconnect();                    
                    break;

                //Handle fragmented messages
                case (byte)SendOption.FragmentedReliable:
                    FragmentedStartMessageReceive(buffer);
                    break;

                case (byte)UdpSendOption.Fragment:
                    FragmentedMessageReceive(buffer);
                    break;

                //Treat everything else as unreliable
                default:
                    InvokeDataReceived(SendOption.None, buffer, 1);
                    Statistics.LogUnreliableReceive(buffer.Length - 1, buffer.Length);
                    break;
            }
        }

        /// <summary>
        ///     Sends bytes using the unreliable UDP protocol.
        /// </summary>
        /// <param name="sendOption">The SendOption to attach.</param>
        /// <param name="data">The data.</param>
        void UnreliableSend(byte sendOption, byte[] data)
        {
            this.UnreliableSend(sendOption, data, 0, data.Length);
        }

        /// <summary>
        ///     Sends bytes using the unreliable UDP protocol.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="sendOption">The SendOption to attach.</param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        void UnreliableSend(byte sendOption, byte[] data, int offset, int length)
        {
            byte[] bytes = new byte[length + 1];

            //Add message type
            bytes[0] = sendOption;

            //Copy data into new array
            Buffer.BlockCopy(data, offset, bytes, bytes.Length - length, length);

            //Write to connection
            WriteBytesToConnection(bytes);

            Statistics.LogUnreliableSend(length, bytes.Length);
        }

        /// <summary>
        ///     Helper method to invoke the data received event.
        /// </summary>
        /// <param name="sendOption">The send option the message was received with.</param>
        /// <param name="buffer">The buffer received.</param>
        /// <param name="dataOffset">The offset of data in the buffer.</param>
        void InvokeDataReceived(SendOption sendOption, byte[] buffer, int dataOffset)
        {
            byte[] dataBytes = new byte[buffer.Length - dataOffset];
            Buffer.BlockCopy(buffer, dataOffset, dataBytes, 0, dataBytes.Length);
            
            InvokeDataReceived(dataBytes, sendOption);
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

            HandleSend(actualBytes, (byte)UdpSendOption.Hello, acknowledgeCallback);
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
            HandleSend(new byte[0], (byte)UdpSendOption.Disconnect);       //TODO Should disconnect wait for an ack?
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
