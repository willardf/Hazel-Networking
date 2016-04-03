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
    public abstract class UdpConnection : Connection
    {
        /// <summary>
        ///     The packets of data that have been transmitted reliably and not acknowledged.
        /// </summary>
        Dictionary<uint, Packet> reliableDataPacketsSent = new Dictionary<uint, Packet>();

        /// <summary>
        ///     Holds the last ID allocated.
        /// </summary>
        volatile uint lastIDAllocated;

        /// <summary>
        ///     The remote end point of this connection.
        /// </summary>
        public EndPoint RemoteEndPoint { get; protected set; }

        class Packet
        {
            public byte[] Data;
            public DateTime SentTime;

            public Packet(byte[] data, DateTime sentTime)
            {
                Data = data;
                SentTime = sentTime;
            }
        }

        /// <summary>
        ///     Handles the reliable/fragmented/ordered sending from this connection.
        /// </summary>
        /// <param name="data">The data being sent.</param>
        /// <param name="sendOption">The send option.</param>
        /// <returns>The bytes that should actually be sent.</returns>
        protected byte[] HandleSend(byte[] data, SendOption sendOption)
        {
            byte[] bytes = new byte[data.Length + 1];
            int offset = 1;

            if (sendOption == SendOption.Reliable)
            {
                bytes = new byte[data.Length + 5];
                offset = 5;

                lock (reliableDataPacketsSent)
                {
                    //Find an ID not used yet.
                    uint id;

                    do
                        id = ++lastIDAllocated;
                    while (reliableDataPacketsSent.ContainsKey(id));

                    bytes[1] = (byte)(id & 0xFF);
                    bytes[2] = (byte)((id >> 16) & 0xFF);
                    bytes[3] = (byte)((id >> 8) & 0xFF);
                    bytes[4] = (byte)id;

                    //Remember packet
                    reliableDataPacketsSent.Add(id, new Packet(data, DateTime.Now));
                }
            }

            Buffer.BlockCopy(data, 0, bytes, offset, bytes.Length);

            return bytes;
        }
    }
}
