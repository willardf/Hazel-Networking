using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hazel.Udp
{
    partial class UdpConnection
    {
        /// <summary>
        ///     The starting timeout, in miliseconds, at which data will be resent.
        /// </summary>
        /// <remarks>
        ///     For reliable delivery data is resent at specified intervals unless an acknowledgement is received from the 
        ///     receiving device. The ResendTimeout specifies the interval between the packets being resent, each time a packet
        ///     is resent the interval is doubled for that packet until the number of resends exceeds the 
        ///     <see cref="ResendsBeforeDisconnect"/> value.
        /// </remarks>
        public int ResendTimeout { get { return resendTimeout; } set { resendTimeout = value; } }
        private volatile int resendTimeout = 200;        //TODO this based of average ping?

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
        ///     Writes the bytes neccessary for a reliable send and stores the send.
        /// </summary>
        /// <param name="bytes">The byte array to write to.</param>
        /// <param name="ackCallback">The callback to make once the packet has been acknowledged.</param>
        void WriteReliableSendHeader(byte[] bytes, Action ackCallback)
        {
            lock (reliableDataPacketsSent)
            {
                //Find an ID not used yet.
                ushort id;

                do
                    id = ++lastIDAllocated;
                while (reliableDataPacketsSent.ContainsKey(id));

                //Write ID
                bytes[1] = (byte)((id >> 8) & 0xFF);
                bytes[2] = (byte)id;

                //Create packet object
                Packet packet = Packet.GetObject();
                packet.Set(
                    bytes,
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
                                    p.Recycle();
                                    return;
                                }
                            }
                        }

                        WriteBytesToConnection(p.Data);

                        Trace.WriteLine("Resend.");
                    },
                    resendTimeout,
                    ackCallback
                );

                //Remember packet
                reliableDataPacketsSent.Add(id, packet);
            }
        }

        /// <summary>
        ///     Handles receives from reliable packets.
        /// </summary>
        /// <param name="bytes">The buffer containing the data.</param>
        /// <returns>Whether the packet was a new packet or not.</returns>
        bool HandleReliableReceive(byte[] bytes)
        {
            //Get the ID form the packet
            ushort id = (ushort)((bytes[1] << 8) + bytes[2]);

            //Send an acknowledgement
            SendAck(bytes[1], bytes[2]);

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
        void HandleAcknowledgement(byte[] bytes)
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

                    packet.Recycle();

                    reliableDataPacketsSent.Remove(id);
                }
            }
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
                    (byte)SendOptionInternal.Acknowledgement,
                    byte1,
                    byte2
                }
            );
        }
    }
}
