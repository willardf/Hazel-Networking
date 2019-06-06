using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;


namespace Hazel.Udp
{
    partial class UdpConnection
    {

        /// <summary>
        ///     Class to hold packet data
        /// </summary>
        public class PingPacket : IRecyclable
        {
            private static readonly ObjectPool<PingPacket> PacketPool = new ObjectPool<PingPacket>(() => new PingPacket());

            public readonly Stopwatch Stopwatch = new Stopwatch();

            internal static PingPacket GetObject()
            {
                return PacketPool.GetObject();
            }

            public void Recycle()
            {
                Stopwatch.Stop();
                PacketPool.PutObject(this);
            }
        }

        internal ConcurrentDictionary<ushort, PingPacket> activePingPackets = new ConcurrentDictionary<ushort, PingPacket>();

        /// <summary>
        ///     The interval from data being received or transmitted to a keepalive packet being sent in milliseconds.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Keepalive packets serve to close connections when an endpoint abruptly disconnects and to ensure than any
        ///         NAT devices do not close their translation for our argument. By ensuring there is regular contact the
        ///         connection can detect and prevent these issues.
        ///     </para>
        ///     <para>
        ///         The default value is 10 seconds, set to System.Threading.Timeout.Infinite to disable keepalive packets.
        ///     </para>
        /// </remarks>
        public int KeepAliveInterval
        {
            get
            {
                return keepAliveInterval;
            }

            set
            {
                keepAliveInterval = value;

                //Update timer
                ResetKeepAliveTimer();
            }
        }
        int keepAliveInterval = 1500;

        public int MissingPingsUntilDisconnect { get; set; } = 6;
        int pingsSinceAck = 0;

        /// <summary>
        ///     The timer creating keepalive pulses.
        /// </summary>
        Timer keepAliveTimer;

        /// <summary>
        ///     Starts the keepalive timer.
        /// </summary>
        void InitializeKeepAliveTimer()
        {
            keepAliveTimer = new Timer(
                (o) =>
                {
                    if (this.pingsSinceAck >= this.MissingPingsUntilDisconnect)
                    {
                        this.Disconnect($"Sent {this.pingsSinceAck} pings that remote has not responded to.");
                        return;
                    }

                    try
                    {
                        SendPing();
                        this.pingsSinceAck++;
                    }
                    catch
                    {
                        DisposeKeepAliveTimer();
                    }
                },
                null,
                keepAliveInterval,
                keepAliveInterval
            );
        }

        // Pings are special, quasi-reliable packets. 
        // We send them to trigger responses that validate our connection is alive
        // An unacked ping should never be the sole cause of a disconnect.
        // Rather, the responses will reset our pingsSinceAck, enough unacked 
        // pings should cause a disconnect.
        void SendPing()
        {
            ushort id = (ushort)Interlocked.Increment(ref lastIDAllocated);

            byte[] bytes = new byte[3];
            bytes[0] = (byte)UdpSendOption.Ping;
            bytes[1] = (byte)(id >> 8);
            bytes[2] = (byte)id;

            PingPacket pkt;
            if (!this.activePingPackets.TryGetValue(id, out pkt))
            {
                pkt = PingPacket.GetObject();
                if (!this.activePingPackets.TryAdd(id, pkt))
                {
                    throw new Exception("This shouldn't be possible");
                }
            }

            pkt.Stopwatch.Restart();

            WriteBytesToConnection(bytes, bytes.Length);

            Statistics.LogReliableSend(0, bytes.Length);
        }

        /// <summary>
        ///     Resets the keepalive timer to zero.
        /// </summary>
        void ResetKeepAliveTimer()
        {
            try
            {
                keepAliveTimer.Change(keepAliveInterval, keepAliveInterval);
            }
            catch { }
        }

        /// <summary>
        ///     Disposes of the keep alive timer.
        /// </summary>
        void DisposeKeepAliveTimer()
        {
            var timer = this.keepAliveTimer;
            if (timer != null)
            {
                this.keepAliveTimer = null;
                timer.Dispose();
            }

            foreach (var kvp in activePingPackets)
            {
                if (this.activePingPackets.TryRemove(kvp.Key, out var pkt))
                {
                    pkt.Recycle();
                }
            }
        }
    }
}