using System;

namespace Hazel.Udp
{
    /// <summary>
    ///     Represents a connection that uses the UDP protocol.
    /// </summary>
    /// <inheritdoc />
    public abstract partial class UdpConnection : NetworkConnection
    {
        protected static readonly byte[] EmptyDisconnectBytes = new byte[] { (byte)UdpSendOption.Disconnect };

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
        protected abstract void WriteBytesToConnection(byte[] bytes, int length);

        /// <inheritdoc/>
        public override void Send(MessageWriter msg)
        {
            if (this._state != ConnectionState.Connected)
                throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");

            byte[] buffer = new byte[msg.Length];
            Buffer.BlockCopy(msg.Buffer, 0, buffer, 0, msg.Length);

            switch (msg.SendOption)
            {
                case SendOption.Reliable:
                    // Inform keepalive not to send for a while
                    ResetKeepAliveTimer();
                    AttachReliableID(buffer, 1, buffer.Length);
                    WriteBytesToConnection(buffer, buffer.Length);
                    Statistics.LogReliableSend(buffer.Length - 3, buffer.Length);
                    break;

                default:
                    WriteBytesToConnection(buffer, buffer.Length);
                    Statistics.LogUnreliableSend(buffer.Length - 1, buffer.Length);;
                    break;
            }
        }

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
            switch (sendOption)
            {
                case (byte)UdpSendOption.Ping:
                case (byte)SendOption.Reliable:
                case (byte)UdpSendOption.Hello:
                    ReliableSend(sendOption, data, ackCallback);
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
        /// <param name="message">The buffer containing the bytes received.</param>
        protected internal void HandleReceive(MessageReader message, int bytesReceived)
        {
            ushort id;
            switch (message.Buffer[0])
            {
                //Handle reliable receives
                case (byte)SendOption.Reliable:
                    ReliableMessageReceive(message, bytesReceived);
                    break;

                //Handle acknowledgments
                case (byte)UdpSendOption.Acknowledgement:
                    AcknowledgementMessageReceive(message.Buffer);
                    message.Recycle();
                    break;

                //We need to acknowledge hello and ping messages but dont want to invoke any events!
                case (byte)UdpSendOption.Ping:
                    ProcessReliableReceive(message.Buffer, 1, out id);
                    Statistics.LogHelloReceive(message.Length);
                    message.Recycle();
                    break;
                case (byte)UdpSendOption.Hello:
                    ProcessReliableReceive(message.Buffer, 1, out id);
                    Statistics.LogHelloReceive(message.Length);
                    break;

                case (byte)UdpSendOption.Disconnect:
                    message.Offset = 1;
                    message.Position = 0;
                    DisconnectRemote("The remote sent a disconnect request", message);
                    message.Recycle();
                    break;
                    
                //Treat everything else as unreliable
                default:
                    InvokeDataReceived(SendOption.None, message, 1, bytesReceived);
                    Statistics.LogUnreliableReceive(message.Length - 1, message.Length);
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
            WriteBytesToConnection(bytes, bytes.Length);

            Statistics.LogUnreliableSend(length, bytes.Length);
        }

        /// <summary>
        ///     Helper method to invoke the data received event.
        /// </summary>
        /// <param name="sendOption">The send option the message was received with.</param>
        /// <param name="buffer">The buffer received.</param>
        /// <param name="dataOffset">The offset of data in the buffer.</param>
        void InvokeDataReceived(SendOption sendOption, MessageReader buffer, int dataOffset, int bytesReceived)
        {
            buffer.Offset = dataOffset;
            buffer.Length = bytesReceived - dataOffset;
            buffer.Position = 0;

            InvokeDataReceived(buffer, sendOption);
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
                
        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeKeepAliveTimer();
                DisposeReliablePackets();
            }

            base.Dispose(disposing);
        }
    }
}
