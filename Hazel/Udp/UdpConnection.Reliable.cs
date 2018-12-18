using System;
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
        public int ResendTimeout { get { return resendTimeout; } set { resendTimeout = value; } }
        private volatile int resendTimeout = 0;

        /// <summary>
        ///     Holds the last ID allocated.
        /// </summary>
        volatile int lastIDAllocated = ushort.MaxValue + 1;

        /// <summary>
        ///     The packets of data that have been transmitted reliably and not acknowledged.
        /// </summary>
        ConcurrentDictionary<ushort, Packet> reliableDataPacketsSent = new ConcurrentDictionary<ushort, Packet>();

        /// <summary>
        ///     The last packets that were received.
        /// </summary>
        HashSet<ushort> reliableDataPacketsMissing = new HashSet<ushort>();

        /// <summary>
        ///     The packet id that was received last.
        /// </summary>
        volatile ushort reliableReceiveLast = 0;

        /// <summary>
        ///     Has the connection received anything yet
        /// </summary>
        volatile bool hasReceivedSomething = false;

        object PingLock = new object();

        /// <summary>
        ///     Returns the average ping to this endpoint.
        /// </summary>
        /// <remarks>
        ///     This returns the average ping for a one-way trip as calculated from the reliable packets that have been sent 
        ///     and acknowledged by the endpoint.
        /// </remarks>
        public volatile float AveragePingMs = 500;

        /// <summary>
        ///     The maximum times a message should be resent before marking the endpoint as disconnected.
        /// </summary>
        /// <remarks>
        ///     Reliable packets will be resent at an interval defined in <see cref="ResendTimeout"/> for the number of times
        ///     specified here. Once a packet has been retransmitted this number of times and has not been acknowledged the
        ///     connection will be marked as disconnected and the <see cref="Connection.Disconnected">Disconnected</see> event
        ///     will be invoked.
        /// </remarks>
        public int DisconnectTimeout { get { return disconnectTimeout; } set { disconnectTimeout = value; } }
        private volatile int disconnectTimeout = 2500;

        /// <summary>
        ///     Class to hold packet data
        /// </summary>
        public class Packet : IRecyclable, IDisposable
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
            public byte[] Data;
            public Timer Timer;
            public volatile int LastTimeout;
            public Action AckCallback;
            public volatile int Retransmissions;
            public Stopwatch Stopwatch = new Stopwatch();
            
            Packet()
            {

            }
            
            internal void Set(ushort id, byte[] data, Action<Packet> resendAction, int timeout, Action ackCallback)
            {
                this.Id = id;
                this.Data = data;
                
                this.Timer = new Timer(
                    (object obj) => resendAction(this),
                    null, 
                    timeout,
                    Timeout.Infinite
                );

                LastTimeout = timeout;
                AckCallback = ackCallback;
                Retransmissions = 0;

                Stopwatch.Reset();
                Stopwatch.Start();
            }

            /// <summary>
            ///     Returns this object back to the object pool from whence it came.
            /// </summary>
            public void Recycle()
            {
                lock (this)
                {
                    if (this.Timer != null)
                    {
                        this.Id = (ushort)(this.Id - 1);
                        this.Timer.Dispose();
                        this.Timer = null;
                    }
                }

                PacketPool.PutObject(this);
            }

            /// <summary>
            ///     Disposes of this object.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected void Dispose(bool disposing)
            {
                if (disposing)
                {
                    lock (this)
                    {
                        if (this.Timer != null)
                        {
                            this.Id = (ushort)(this.Id - 1);
                            this.Timer.Dispose();
                            this.Timer = null;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Adds a 2 byte ID to the packet at offset and stores the packet reference for retransmission.
        /// </summary>
        /// <param name="buffer">The buffer to attach to.</param>
        /// <param name="offset">The offset to attach at.</param>
        /// <param name="ackCallback">The callback to make once the packet has been acknowledged.</param>
        void AttachReliableID(byte[] buffer, int offset, int sendLength, Action ackCallback = null)
        {
            //Find and reliable ID
            lock (reliableDataPacketsSent)
            {
                //Find an ID not used yet.
                ushort id;

                //Create packet object
                Packet packet = Packet.GetObject();

                do
                {
                    id = (ushort)Interlocked.Increment(ref lastIDAllocated);
                }
                while (!reliableDataPacketsSent.TryAdd(id, packet));

                //Write ID
                buffer[offset] = (byte)((id >> 8) & 0xFF);
                buffer[offset + 1] = (byte)id;

                packet.Set(
                    id,
                    buffer,
                    (Packet p) =>
                    {
                        Packet self;
                        if (p.Stopwatch.ElapsedMilliseconds > this.disconnectTimeout)
                        {
                            if (reliableDataPacketsSent.TryRemove(p.Id, out self))
                            {
                                HandleDisconnect(new HazelException($"Reliable packet {self.Id} was not ack'd after {self.Retransmissions} resends"));

                                self.Recycle();
                            }

                            return;
                        }

                        lock (p)
                        {
                            // Callback for a previous packet
                            if (p.Id != id) return;

                            // Backoff retry frequency to avoid congestion
                            p.LastTimeout = (int)Math.Min(p.LastTimeout * 1.25f, 2000);
                            p.Timer.Change(p.LastTimeout, Timeout.Infinite);
                        }

                        try
                        {
                            WriteBytesToConnection(p.Data, sendLength);
                            p.Retransmissions++;
                        }
                        catch (InvalidOperationException e)
                        {
                            //No longer connected
                            HandleDisconnect(new HazelException("Could not resend data as connection is no longer connected", e));
                        }

                        Trace.WriteLine("Resend.");
                    },
                    resendTimeout > 0 ? resendTimeout : (int)Math.Max(500, Math.Min(AveragePingMs * 4, 2000)),
                    ackCallback
                );
            }
        }

        /// <summary>
        ///     Sends the bytes reliably and stores the send.
        /// </summary>
        /// <param name="sendOption"></param>
        /// <param name="data">The byte array to write to.</param>
        /// <param name="ackCallback">The callback to make once the packet has been acknowledged.</param>
        void ReliableSend(byte sendOption, byte[] data, Action ackCallback = null)
        {
            this.ReliableSend(sendOption, data, 0, data.Length, ackCallback);
        }

        /// <summary>
        ///     Sends the bytes reliably and stores the send.
        /// </summary>
        /// <param name="sendOption"></param>
        /// <param name="data">The byte array to write to.</param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <param name="ackCallback">The callback to make once the packet has been acknowledged.</param>
        void ReliableSend(byte sendOption, byte[] data, int offset, int length, Action ackCallback = null)
        {
            //Inform keepalive not to send for a while
            ResetKeepAliveTimer();

            byte[] bytes = new byte[length + 3];

            //Add message type
            bytes[0] = sendOption;

            //Add reliable ID
            AttachReliableID(bytes, 1, bytes.Length, ackCallback);

            //Copy data into new array
            Buffer.BlockCopy(data, offset, bytes, bytes.Length - length, length);

            //Write to connection
            WriteBytesToConnection(bytes, bytes.Length);

            Statistics.LogReliableSend(length, bytes.Length);
        }

        void ReliableSend(byte sendOption)
        {
            byte[] bytes = new byte[3];
            bytes[0] = sendOption;

            //Add reliable ID
            AttachReliableID(bytes, 1, bytes.Length, null);

            //Write to connection
            WriteBytesToConnection(bytes, bytes.Length);

            Statistics.LogReliableSend(0, bytes.Length);
        }

        /// <summary>
        ///     Handles a reliable message being received and invokes the data event.
        /// </summary>
        /// <param name="message">The buffer received.</param>
        void ReliableMessageReceive(MessageReader message, int bytesReceived)
        {
            ushort id;
            if (ProcessReliableReceive(message.Buffer, 1, out id))
            {
                InvokeDataReceived(SendOption.Reliable, message, 3, bytesReceived, id);
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
        bool ProcessReliableReceive(byte[] bytes, int offset, out ushort id)
        {
            byte b1 = bytes[offset];
            byte b2 = bytes[offset + 1];

            //Get the ID form the packet
            id = (ushort)((b1 << 8) + b2);

            //Send an acknowledgement
            SendAck(b1, b2);

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
                if (isNew || !hasReceivedSomething)
                {
                    //Mark items between the most recent receive and the id received as missing
                    for (ushort i = (ushort)(reliableReceiveLast + 1); i < id; i++)
                    {
                        reliableDataPacketsMissing.Add(i);
                    }

                    //Update the most recently received
                    reliableReceiveLast = id;
                    hasReceivedSomething = true;
                }
                
                //Else it could be a missing packet
                else
                {
                    //See if we're missing it, else this packet is a duplicate as so we return false
                    if (!reliableDataPacketsMissing.Remove(id))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        ///     Handles acknowledgement packets to us.
        /// </summary>
        /// <param name="bytes">The buffer containing the data.</param>
        void AcknowledgementMessageReceive(byte[] bytes)
        {
            //Get ID
            ushort id = (ushort)((bytes[1] << 8) + bytes[2]);

            //Dispose of timer and remove from dictionary
            Packet packet;
            if (reliableDataPacketsSent.TryRemove(id, out packet))
            {
                float rt = packet.Stopwatch.ElapsedMilliseconds;

                packet.AckCallback?.Invoke();
                packet.Recycle();

                lock (PingLock)
                {
                    this.AveragePingMs = Math.Max(50, this.AveragePingMs * .7f + rt * .3f);
                }
            }

            Statistics.LogReliableReceive(0, bytes.Length);
        }

        /// <summary>
        ///     Sends an acknowledgement for a packet given its identification bytes.
        /// </summary>
        /// <param name="byte1">The first identification byte.</param>
        /// <param name="byte2">The second identification byte.</param>
        internal void SendAck(byte byte1, byte byte2)
        {
            byte[] bytes = new byte[]
            {
                (byte)UdpSendOption.Acknowledgement,
                byte1,
                byte2
            };

            // Always reply with acknowledgement in order to stop the sender repeatedly sending it
            // TODO: group acks together
            try
            {
                WriteBytesToConnection(bytes, bytes.Length);
            }
            catch (InvalidOperationException) { }
        }

        void DisposeReliablePackets()
        {
            var keys = this.reliableDataPacketsSent.Keys.ToArray();
            foreach (var k in keys)
            {
                Packet pkt;
                if (this.reliableDataPacketsSent.TryRemove(k, out pkt))
                {
                    pkt.Recycle();
                }
            }
        }
    }
}
