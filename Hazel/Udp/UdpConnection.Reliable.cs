using System;
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
        ///         is resent the interval is doubled for that packet until the number of resends exceeds the 
        ///         <see cref="ResendsBeforeDisconnect"/> value.
        ///     </para>
        ///     <para>
        ///         Setting this to its default of 0 will mean the timout is 4 times the value of the average ping, usually 
        ///         resulting in a more dynamic resend that responds to endpoints on slower or faster connections.
        ///     </para>
        /// </remarks>
        public int ResendTimeout { get { return resendTimeout; } set { resendTimeout = value; } }
        private volatile int resendTimeout = 0;

        /// <summary>
        ///     Holds the last ID allocated.
        /// </summary>
        volatile ushort lastIDAllocated;

        /// <summary>
        ///     The packets of data that have been transmitted reliably and not acknowledged.
        /// </summary>
        Dictionary<ushort, Packet> reliableDataPacketsSent = new Dictionary<ushort, Packet>();

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

        /// <summary>
        ///     The total time it has taken reliable packets to make a round trip.
        /// </summary>
        long totalRoundTime = 0;

        /// <summary>
        ///     The number of reliable messages that have been sent.
        /// </summary>
        long totalReliableMessages = 0;

        /// <summary>
        ///     Returns the average ping to this endpoint.
        /// </summary>
        /// <remarks>
        ///     This returns the average ping for a one-way trip as calculated from the reliable packets that have been sent 
        ///     and acknowledged by the endpoint.
        /// </remarks>
        public double AveragePing
        {
            get
            {
                long t = Interlocked.Read(ref totalReliableMessages);
                if (t == 0)
                    return 0;
                else
                    return Interlocked.Read(ref totalRoundTime) / t / 2;
            }
        }

        /// <summary>
        ///     The maximum times a message should be resent before marking the endpoint as disconnected.
        /// </summary>
        /// <remarks>
        ///     Reliable packets will be resent at an interval defined in <see cref="ResendTimeout"/> for the number of times
        ///     specified here. Once a packet has been retransmitted this number of times and has not been acknowledged the
        ///     connection will be marked as disconnected and the <see cref="Connection.Disconnected">Disconnected</see> event
        ///     will be invoked.
        /// </remarks>
        public int ResendsBeforeDisconnect { get { return resendsBeforeDisconnect; } set { resendsBeforeDisconnect = value; } }
        private volatile int resendsBeforeDisconnect = 3;

        /// <summary>
        ///     Class to hold packet data
        /// </summary>
        class Packet : IRecyclable, IDisposable
        {
            /// <summary>
            ///     Object pool for this event.
            /// </summary>
            static readonly ObjectPool<Packet> objectPool = new ObjectPool<Packet>(() => new Packet());

            /// <summary>
            ///     Returns an instance of this object from the pool.
            /// </summary>
            /// <returns></returns>
            internal static Packet GetObject()
            {
                return objectPool.GetObject();
            }

            public byte[] Data;
            public Timer Timer;
            public volatile int LastTimeout;
            public Action AckCallback;
            public volatile bool Acknowledged;
            public volatile int Retransmissions;
            public Stopwatch Stopwatch = new Stopwatch();

            Packet()
            {

            }
            
            internal void Set(byte[] data, Action<Packet> resendAction, int timeout, Action ackCallback)
            {
                Data = data;
                
                Timer = new Timer(
                    (object obj) => resendAction(this),
                    null, 
                    timeout,
                    Timeout.Infinite
                );

                LastTimeout = timeout;
                AckCallback = ackCallback;
                Acknowledged = false;
                Retransmissions = 0;

                Stopwatch.Reset();
                Stopwatch.Start();
            }

            /// <summary>
            ///     Returns this object back to the object pool from whence it came.
            /// </summary>
            public void Recycle()
            {
                lock (Timer)
                    Timer.Dispose();

                objectPool.PutObject(this);
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
                    lock (Timer)
                        Timer.Dispose();
                }
            }
        }

        /// <summary>
        ///     Adds a 2 byte ID to the packet at offset and stores the packet reference for retransmission.
        /// </summary>
        /// <param name="buffer">The buffer to attach to.</param>
        /// <param name="offset">The offset to attach at.</param>
        /// <param name="ackCallback">The callback to make once the packet has been acknowledged.</param>
        void AttachReliableID(byte[] buffer, int offset, Action ackCallback = null)
        {
            //Find and reliable ID
            lock (reliableDataPacketsSent)
            {
                //Find an ID not used yet.
                ushort id;

                do
                    id = ++lastIDAllocated;
                while (reliableDataPacketsSent.ContainsKey(id));

                //Write ID
                buffer[offset] = (byte)((id >> 8) & 0xFF);
                buffer[offset + 1] = (byte)id;

                //Create packet object
                Packet packet = Packet.GetObject();
                packet.Set(
                    buffer,
                    (Packet p) =>
                    {
                        //Double packet timeout
                        lock (p.Timer)
                        {
                            if (!p.Acknowledged)
                            {
                                p.Timer.Change(p.LastTimeout *= 2, Timeout.Infinite);
                                if (++p.Retransmissions > ResendsBeforeDisconnect)
                                {
                                    HandleDisconnect();

                                    //Set acknowledged so we dont change the timer again
                                    p.Acknowledged = true;

                                    p.Recycle();
                                    return;
                                }
                            }
                        }

                        try
                        {
                            WriteBytesToConnection(p.Data);
                        }
                        catch (InvalidOperationException e)
                        {
                            //No longer connected
                            HandleDisconnect(new HazelException("Could not resend data as connection is no longer connected", e));
                        }

                        Trace.WriteLine("Resend.");
                    },
                    resendTimeout > 0 ? resendTimeout : (AveragePing != 0 ? (int)AveragePing * 4 : 200),
                    ackCallback
                );

                //Remember packet
                reliableDataPacketsSent.Add(id, packet);
            }
        }

        /// <summary>
        ///     Sends the bytes reliably and stores the send.
        /// </summary>
        /// <param name="bytes">The byte array to write to.</param>
        /// <param name="ackCallback">The callback to make once the packet has been acknowledged.</param>
        void ReliableSend(byte sendOption, byte[] data, Action ackCallback = null)
        {
            byte[] bytes = new byte[data.Length + 3];

            //Add message type
            bytes[0] = sendOption;

            //Add reliable ID
            AttachReliableID(bytes, 1, ackCallback);
            
            //Copy data into new array
            Buffer.BlockCopy(data, 0, bytes, bytes.Length - data.Length, data.Length);

            //Write to connection
            WriteBytesToConnection(bytes);

            Statistics.LogReliableSend(data.Length, bytes.Length);
        }

        /// <summary>
        ///     Handles a reliable message being received and invokes the data event.
        /// </summary>
        /// <param name="buffer">The buffer received.</param>
        void ReliableMessageReceive(byte[] buffer)
        {
            if (ProcessReliableReceive(buffer, 1))
                InvokeDataReceived(SendOption.Reliable, buffer, 3);

            Statistics.LogReliableReceive(buffer.Length - 3, buffer.Length);
        }

        /// <summary>
        ///     Handles receives from reliable packets.
        /// </summary>
        /// <param name="bytes">The buffer containing the data.</param>
        /// <param name="offset">The offset of the reliable header.</param>
        /// <returns>Whether the packet was a new packet or not.</returns>
        bool ProcessReliableReceive(byte[] bytes, int offset)
        {
            //Get the ID form the packet
            ushort id = (ushort)((bytes[offset] << 8) + bytes[offset + 1]);

            //Send an acknowledgement
            SendAck(bytes[offset], bytes[offset + 1]);

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
                        reliableDataPacketsMissing.Add(i);

                    //Update the most recently received
                    reliableReceiveLast = id;
                    hasReceivedSomething = true;
                }
                
                //Else it could be a missing packet
                else
                {
                    //See if we're missing it, else this packet is a duplicate as so we return false
                    if (reliableDataPacketsMissing.Contains(id))
                        reliableDataPacketsMissing.Remove(id);
                    else
                        return false;
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

            lock (reliableDataPacketsSent)
            {
                //Dispose of timer and remove from dictionary
                if (reliableDataPacketsSent.ContainsKey(id))
                {
                    Packet packet = reliableDataPacketsSent[id];
                    
                    packet.Acknowledged = true;

                    if (packet.AckCallback != null)
                        packet.AckCallback.Invoke();

                    //Add to average ping
                    packet.Stopwatch.Stop();
                    Interlocked.Add(ref totalRoundTime, packet.Stopwatch.Elapsed.Milliseconds);
                    Interlocked.Increment(ref totalReliableMessages);

                    packet.Recycle();

                    reliableDataPacketsSent.Remove(id);
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
            //Always reply with acknowledgement in order to stop the sender repeatedly sending it
            WriteBytesToConnection(     //TODO group acks together
                new byte[]
                {
                    (byte)UdpSendOption.Acknowledgement,
                    byte1,
                    byte2
                }
            );
        }
    }
}
