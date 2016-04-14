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

        /// <summary>
        ///     Handles the reliable/fragmented/ordered sending from this connection.
        /// </summary>
        /// <param name="data">The data being sent.</param>
        /// <param name="sendOption">The send option.</param>
        /// <returns>The bytes that should actually be sent.</returns>
        protected void HandleSend(byte[] data, SendOption sendOption)
        {
            byte[] bytes;
            switch (sendOption)
            {
                case SendOption.Reliable:
                    bytes = new byte[data.Length + 3];
                    WriteReliableSendHeader(bytes);
                    break;

                default:
                    bytes = new byte[data.Length + 1];
                    break;
            }

            //Add message type
            bytes[0] = (byte)sendOption;

            //Copy data into new array
            Buffer.BlockCopy(data, 0, bytes, bytes.Length - data.Length, data.Length);

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
            int headerSize = 1;
            switch (buffer[0])
            {
                case (byte)SendOption.Reliable:
                    headerSize = 3;

                    if (HandleReliableReceive(buffer) == false)
                        return null;
                    break;

                case (byte)SendOptionInternal.Acknowledgement:
                    HandleAcknowledgement(buffer);
                    
                    Statistics.LogReceive(0, bytesReceived);
                    
                    return null;
            }

            byte[] dataBytes = new byte[bytesReceived - headerSize];
            Buffer.BlockCopy(buffer, headerSize, dataBytes, 0, dataBytes.Length);

            Statistics.LogReceive(dataBytes.Length, bytesReceived);

            return dataBytes;
        }
    }
}
