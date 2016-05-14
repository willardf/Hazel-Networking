using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

/* 
* Copyright (C) Jamie Read - All Rights Reserved
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential
* Written by Jamie Read <jamie.read@outlook.com>, January 2016
*/

namespace Hazel
{
    /// <summary>
    ///     Represents a connection that uses the UDP protocol.
    /// </summary>
    public abstract partial class UdpConnection : Connection
    {
        /// <summary>
        ///     The remote end point of this connection.
        /// </summary>
        public EndPoint RemoteEndPoint { get; protected set; }

        /// <summary>
        ///     Writes the given bytes to the connection.
        /// </summary>
        /// <param name="bytes">The bytes to write.</param>
        protected abstract void WriteBytesToConnection(byte[] bytes);

        protected UdpConnection()
        {
            InitializeKeepAliveTimer();
        }

        /// <summary>
        ///     Handles the reliable/fragmented/ordered sending from this connection.
        /// </summary>
        /// <param name="data">The data being sent.</param>
        /// <param name="sendOption">The send option as a byte.</param>
        /// <returns>The bytes that should actually be sent.</returns>
        protected void HandleSend(byte[] data, byte sendOption, Action ackCallback = null)
        {
            byte[] bytes;
            switch (sendOption)
            {
                //Handle reliable header
                case (byte)SendOption.Reliable:
                    bytes = new byte[data.Length + 3];
                    WriteReliableSendHeader(bytes, ackCallback);
                    break;

                //Handle hellos (ignore data)
                case (byte)SendOptionInternal.Hello:
                    bytes = new byte[3];
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
        /// <param name="buffer">The array of the data received.</param>
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
        protected void SendHello(Action acknowledgeCallback)
        {
            HandleSend(new byte[0], (byte)SendOptionInternal.Hello, acknowledgeCallback);
        }

        /// <summary>
        ///     Closes this connection safely.
        /// </summary>
        public override void Close()
        {
            HandleSend(new byte[0], (byte)SendOptionInternal.Disconnect);       //TODO Should disconnect wait for an ack?

            base.Close();
        }

        /// <summary>
        ///     Called when the socket has been disconnected at the remote host.
        /// </summary>
        /// <param name="e">The exception if one was the cause.</param>
        protected abstract void HandleDisconnect(HazelException e = null);

        /// <summary>
        ///     Called when things are being disposed of
        /// </summary>
        /// <param name="disposing"></param>
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
