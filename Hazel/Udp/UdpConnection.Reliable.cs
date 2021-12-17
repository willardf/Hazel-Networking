﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;


namespace Hazel.Udp
{
    partial class UdpConnection
    {
        /// <summary>
        ///     The starting timeout, in miliseconds, at which data will be resent.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         For reliable delivery data is resent at specified intervals unless an acknowledgement is received from the 
        ///         receiving device. The ResendTimeout specifies the interval between the packets being resent, each time a packet
        ///         is resent the interval is increased for that packet until the duration exceeds the <see cref="DisconnectTimeout"/> value.
        ///     </para>
        ///     <para>
        ///         Setting this to its default of 0 will mean the timeout is 2 times the value of the average ping, usually 
        ///         resulting in a more dynamic resend that responds to endpoints on slower or faster connections.
        ///     </para>
        /// </remarks>
        public volatile int ResendTimeout = 0;

        /// <summary>
        /// Max number of times to resend. 0 == no limit
        /// </summary>
        public volatile int ResendLimit = 0;

        /// <summary>
        /// A compounding multiplier to back off resend timeout.
        /// Applied to ping before first timeout when ResendTimeout == 0.
        /// </summary>
        public volatile float ResendPingMultiplier = 2;

        /// <summary>
        ///     Holds the last ID allocated.
        /// </summary>
        private int lastIDAllocated = 0;

        /// <summary>
        ///     The packets of data that have been transmitted reliably and not acknowledged.
        /// </summary>
        internal ConcurrentDictionary<ushort, Packet> reliableDataPacketsSent = new ConcurrentDictionary<ushort, Packet>();

        /// <summary>
        ///     Packet ids that have not been received, but are expected. 
        /// </summary>
        private HashSet<ushort> reliableDataPacketsMissing = new HashSet<ushort>();

        /// <summary>
        ///     The packet id that was received last.
        /// </summary>
        protected volatile ushort reliableReceiveLast = ushort.MaxValue;

        private object PingLock = new object();

        /// <summary>
        ///     Returns the average ping to this endpoint.
        /// </summary>
        /// <remarks>
        ///     This returns the average ping for a one-way trip as calculated from the reliable packets that have been sent 
        ///     and acknowledged by the endpoint.
        /// </remarks>
        private float _pingMs = 500;

        /// <summary>
        ///     The maximum times a message should be resent before marking the endpoint as disconnected.
        /// </summary>
        /// <remarks>
        ///     Reliable packets will be resent at an interval defined in <see cref="ResendTimeout"/> for the number of times
        ///     specified here. Once a packet has been retransmitted this number of times and has not been acknowledged the
        ///     connection will be marked as disconnected and the <see cref="Connection.Disconnected">Disconnected</see> event
        ///     will be invoked.
        /// </remarks>
        public volatile int DisconnectTimeout = 5000;

        /// <summary>
        ///     Class to hold packet data
        /// </summary>
        public class Packet : IRecyclable
        {
            /// <summary>
            ///     Object pool for this event.
            /// </summary>
            public static readonly ObjectPool<Packet> PacketPool = new ObjectPool<Packet>(() => new Packet());

            /// <summary>
            ///     Returns an instance of this object from the pool.
            /// </summary>
            /// <returns></returns>
            internal static Packet GetObject()
            {
                return PacketPool.GetObject();
            }

            public ushort Id;
            private byte[] Data;
            private UdpConnection Connection;
            private int Length;

            public int NextTimeout;
            public volatile bool Acknowledged;

            public Action AckCallback;

            public int Retransmissions;
            public Stopwatch Stopwatch = new Stopwatch();

            Packet()
            {
            }

            internal void Set(ushort id, UdpConnection connection, byte[] data, int length, int timeout, Action ackCallback)
            {
                this.Id = id;
                this.Data = data;
                this.Connection = connection;
                this.Length = length;

                this.Acknowledged = false;
                this.NextTimeout = timeout;
                this.AckCallback = ackCallback;
                this.Retransmissions = 0;

                this.Stopwatch.Restart();
            }

            // Packets resent
            public int Resend()
            {
                var connection = this.Connection;
                if (!this.Acknowledged && connection != null)
                {
                    long lifetime = this.Stopwatch.ElapsedMilliseconds;
                    if (lifetime >= connection.DisconnectTimeout)
                    {
                        if (connection.reliableDataPacketsSent.TryRemove(this.Id, out Packet self))
                        {
                            connection.DisconnectInternal(HazelInternalErrors.ReliablePacketWithoutResponse, $"Reliable packet {self.Id} (size={this.Length}) was not ack'd after {lifetime}ms ({self.Retransmissions} resends)");

                            self.Recycle();
                        }

                        return 0;
                    }

                    if (lifetime >= this.NextTimeout)
                    {
                        ++this.Retransmissions;
                        if (connection.ResendLimit != 0
                            && this.Retransmissions > connection.ResendLimit)
                        {
                            if (connection.reliableDataPacketsSent.TryRemove(this.Id, out Packet self))
                            {
                                connection.DisconnectInternal(HazelInternalErrors.ReliablePacketWithoutResponse, $"Reliable packet {self.Id} (size={this.Length}) was not ack'd after {self.Retransmissions} resends ({lifetime}ms)");

                                self.Recycle();
                            }

                            return 0;
                        }

                        this.NextTimeout += (int)Math.Min(this.NextTimeout * connection.ResendPingMultiplier, 1000);
                        try
                        {
                            connection.WriteBytesToConnection(this.Data, this.Length);
                            connection.Statistics.LogMessageResent();
                            return 1;
                        }
                        catch (InvalidOperationException)
                        {
                            connection.DisconnectInternal(HazelInternalErrors.ConnectionDisconnected, "Could not resend data as connection is no longer connected");
                        }
                    }
                }

                return 0;
            }

            /// <summary>
            ///     Returns this object back to the object pool from whence it came.
            /// </summary>
            public void Recycle()
            {
                this.Acknowledged = true;
                this.Connection = null;

                PacketPool.PutObject(this);
            }
        }

        internal int ManageReliablePackets()
        {
            int output = 0;
            if (this.reliableDataPacketsSent.Count > 0)
            {
                foreach (var kvp in this.reliableDataPacketsSent)
                {
                    Packet pkt = kvp.Value;

                    try
                    {
                        output += pkt.Resend();
                    }
                    catch { }
                }
            }

            return output;
        }

        /// <summary>
        ///     Adds a 2 byte ID to the packet at offset and stores the packet reference for retransmission.
        /// </summary>
        /// <param name="buffer">The buffer to attach to.</param>
        /// <param name="offset">The offset to attach at.</param>
        /// <param name="ackCallback">The callback to make once the packet has been acknowledged.</param>
        protected ushort AttachReliableID(byte[] buffer, int offset, Action ackCallback = null)
        {
            ushort id = (ushort)Interlocked.Increment(ref lastIDAllocated);

            buffer[offset] = (byte)(id >> 8);
            buffer[offset + 1] = (byte)id;

            Packet packet = Packet.GetObject();
            packet.Set(
                id,
                this,
                buffer,
                buffer.Length,
                ResendTimeout > 0 ? ResendTimeout : (int)Math.Min(_pingMs * this.ResendPingMultiplier, 300),
                ackCallback);

            if (!reliableDataPacketsSent.TryAdd(id, packet))
            {
                throw new Exception("That shouldn't be possible");
            }

            return id;
        }

        public static int ClampToInt(float value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return (int)value;
        }

        /// <summary>
        ///     Sends the bytes reliably and stores the send.
        /// </summary>
        /// <param name="sendOption"></param>
        /// <param name="data">The byte array to write to.</param>
        /// <param name="ackCallback">The callback to make once the packet has been acknowledged.</param>
        private void ReliableSend(byte sendOption, byte[] data, Action ackCallback = null)
        {
            if (data.Length >= Mtu)
            {
                FragmentedSend(sendOption, data, ackCallback);
                return;
            }

            //Inform keepalive not to send for a while
            ResetKeepAliveTimer();

            byte[] bytes = new byte[data.Length + 3];

            //Add message type
            bytes[0] = sendOption;

            //Add reliable ID
            AttachReliableID(bytes, 1, ackCallback);

            //Copy data into new array
            Buffer.BlockCopy(data, 0, bytes, bytes.Length - data.Length, data.Length);

            //Write to connection
            WriteBytesToConnection(bytes, bytes.Length);

            Statistics.LogReliableSend(data.Length, bytes.Length);
        }

        /// <summary>
        ///     Handles a reliable message being received and invokes the data event.
        /// </summary>
        /// <param name="message">The buffer received.</param>
        private void ReliableMessageReceive(MessageReader message, int bytesReceived)
        {
            ushort id;
            if (ProcessReliableReceive(message.Buffer, 1, out id))
            {
                InvokeDataReceived(SendOption.Reliable, message, 3, bytesReceived);
            }
            else
            {
                message.Recycle();
            }

            Statistics.LogReliableReceive(message.Length - 3, message.Length);
        }

        /// <summary>
        ///     Handles receives from reliable packets.
        /// </summary>
        /// <param name="bytes">The buffer containing the data.</param>
        /// <param name="offset">The offset of the reliable header.</param>
        /// <returns>Whether the packet was a new packet or not.</returns>
        private bool ProcessReliableReceive(byte[] bytes, int offset, out ushort id)
        {
            byte b1 = bytes[offset];
            byte b2 = bytes[offset + 1];

            //Get the ID form the packet
            id = (ushort)((b1 << 8) + b2);

            /*
             * It gets a little complicated here (note the fact I'm actually using a multiline comment for once...)
             * 
             * In a simple world if our data is greater than the last reliable packet received (reliableReceiveLast)
             * then it is guaranteed to be a new packet, if it's not we can see if we are missing that packet (lookup 
             * in reliableDataPacketsMissing).
             * 
             * --------rrl#############             (1)
             * 
             * (where --- are packets received already and #### are packets that will be counted as new)
             * 
             * Unfortunately if id becomes greater than 65535 it will loop back to zero so we will add a pointer that
             * specifies any packets with an id behind it are also new (overwritePointer).
             * 
             * ####op----------rrl#####             (2)
             * 
             * ------rll#########op----             (3)
             * 
             * Anything behind than the reliableReceiveLast pointer (but greater than the overwritePointer is either a 
             * missing packet or something we've already received so when we change the pointers we need to make sure 
             * we keep note of what hasn't been received yet (reliableDataPacketsMissing).
             * 
             * So...
             */

            bool result = true;
            
            lock (reliableDataPacketsMissing)
            {
                //Calculate overwritePointer
                ushort overwritePointer = (ushort)(reliableReceiveLast - 32768);

                //Calculate if it is a new packet by examining if it is within the range
                bool isNew;
                if (overwritePointer < reliableReceiveLast)
                    isNew = id > reliableReceiveLast || id <= overwritePointer;     //Figure (2)
                else
                    isNew = id > reliableReceiveLast && id <= overwritePointer;     //Figure (3)
                
                //If it's new or we've not received anything yet
                if (isNew)
                {
                    // Mark items between the most recent receive and the id received as missing
                    if (id > reliableReceiveLast)
                    {
                        for (ushort i = (ushort)(reliableReceiveLast + 1); i < id; i++)
                        {
                            reliableDataPacketsMissing.Add(i);
                        }
                    }
                    else
                    {
                        int cnt = (ushort.MaxValue - reliableReceiveLast) + id;
                        for (ushort i = 1; i <= cnt; ++i)
                        {
                            reliableDataPacketsMissing.Add((ushort)(i + reliableReceiveLast));
                        }
                    }

                    //Update the most recently received
                    reliableReceiveLast = id;
                }
                
                //Else it could be a missing packet
                else
                {
                    //See if we're missing it, else this packet is a duplicate as so we return false
                    if (!reliableDataPacketsMissing.Remove(id))
                    {
                        result = false;
                    }
                }
            }

            // Send an acknowledgement
            SendAck(id);

            return result;
        }

        /// <summary>
        ///     Handles acknowledgement packets to us.
        /// </summary>
        /// <param name="bytes">The buffer containing the data.</param>
        private void AcknowledgementMessageReceive(byte[] bytes, int bytesReceived)
        {
            this.pingsSinceAck = 0;

            ushort id = (ushort)((bytes[1] << 8) + bytes[2]);
            AcknowledgeMessageId(id);

            if (bytesReceived == 4)
            {
                byte recentPackets = bytes[3];
                for (int i = 1; i <= 8; ++i)
                {
                    if ((recentPackets & 1) != 0)
                    {
                        AcknowledgeMessageId((ushort)(id - i));
                    }

                    recentPackets >>= 1;
                }
            }

            Statistics.LogReliableReceive(0, bytesReceived);
        }

        protected void AcknowledgeMessageId(ushort id)
        {
            // Dispose of timer and remove from dictionary
            if (reliableDataPacketsSent.TryRemove(id, out Packet packet))
            {
                float rt = packet.Stopwatch.ElapsedMilliseconds;

                packet.AckCallback?.Invoke();
                packet.Recycle();

                lock (PingLock)
                {
                    this._pingMs = Math.Max(50, this._pingMs * .7f + rt * .3f);
                }
            }
            else if (this.activePingPackets.TryRemove(id, out PingPacket pingPkt))
            {
                float rt = pingPkt.Stopwatch.ElapsedMilliseconds;

                pingPkt.Recycle();

                lock (PingLock)
                {
                    this._pingMs = Math.Max(50, this._pingMs * .7f + rt * .3f);
                }
            }
        }

        /// <summary>
        ///     Sends an acknowledgement for a packet given its identification bytes.
        /// </summary>
        /// <param name="byte1">The first identification byte.</param>
        /// <param name="byte2">The second identification byte.</param>
        private void SendAck(ushort id)
        {
            byte recentPackets = 0;
            lock (this.reliableDataPacketsMissing)
            {
                for (int i = 1; i <= 8; ++i)
                {
                    if (!this.reliableDataPacketsMissing.Contains((ushort)(id - i)))
                    {
                        recentPackets |= (byte)(1 << (i - 1));
                    }
                }
            }

            byte[] bytes = new byte[]
            {
                (byte)UdpSendOption.Acknowledgement,
                (byte)(id >> 8),
                (byte)(id >> 0),
                recentPackets
            };

            try
            {
                WriteBytesToConnection(bytes, bytes.Length);
            }
            catch (InvalidOperationException) { }
        }

        private void DisposeReliablePackets()
        {
            foreach (var kvp in reliableDataPacketsSent)
            {
                if (this.reliableDataPacketsSent.TryRemove(kvp.Key, out var pkt))
                {
                    pkt.Recycle();
                }
            }
        }
    }
}
