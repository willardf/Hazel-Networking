﻿using System;
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
                ResetKeepAliveTimer();
            }
        }
        private int keepAliveInterval = 1500;

        public int MissingPingsUntilDisconnect { get; set; } = 6;
        private volatile int pingsSinceAck = 0;

        /// <summary>
        ///     The timer creating keepalive pulses.
        /// </summary>
        private Timer keepAliveTimer;

        /// <summary>
        ///     Starts the keepalive timer.
        /// </summary>
        protected void InitializeKeepAliveTimer()
        {
            keepAliveTimer = new Timer(
                HandleKeepAlive,
                null,
                keepAliveInterval,
                keepAliveInterval
            );
        }

        private void HandleKeepAlive(object state)
        {
            if (this.State != ConnectionState.Connected) return;

            if (this.pingsSinceAck >= this.MissingPingsUntilDisconnect)
            {
                this.DisposeKeepAliveTimer();
                this.DisconnectInternal(HazelInternalErrors.PingsWithoutResponse, $"Sent {this.pingsSinceAck} pings that remote has not responded to.");
                return;
            }

            try
            {
                this.pingsSinceAck++;
                SendPing();
            }
            catch
            {
            }
        }

        // Pings are special, quasi-reliable packets. 
        // We send them to trigger responses that validate our connection is alive
        // An unacked ping should never be the sole cause of a disconnect.
        // Rather, the responses will reset our pingsSinceAck, enough unacked 
        // pings should cause a disconnect.
        private void SendPing()
        {
            ushort id = (ushort)Interlocked.Increment(ref lastIDAllocated);

            using SmartBuffer buffer = this.bufferPool.GetObject();
            buffer.Length = 3;
            buffer[0] = (byte)UdpSendOption.Ping;
            buffer[1] = (byte)(id >> 8);
            buffer[2] = (byte)id;

            if (!this.activePingPackets.TryGetValue(id, out var ping))
            {
                ping = PingPacket.GetObject();
                if (!this.activePingPackets.TryAdd(id, ping))
                {
                    throw new Exception("This shouldn't be possible");
                }
            }

            ping.Stopwatch.Restart();

            WriteBytesToConnection(buffer, buffer.Length);
            Statistics.LogPingSend(buffer.Length);
        }

        /// <summary>
        ///     Resets the keepalive timer to zero.
        /// </summary>
        protected void ResetKeepAliveTimer()
        {
            try
            {
                keepAliveTimer?.Change(keepAliveInterval, keepAliveInterval);
            }
            catch { }
        }

        /// <summary>
        ///     Disposes of the keep alive timer.
        /// </summary>
        private void DisposeKeepAliveTimer()
        {
            if (this.keepAliveTimer != null)
            {
                this.keepAliveTimer.Dispose();
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