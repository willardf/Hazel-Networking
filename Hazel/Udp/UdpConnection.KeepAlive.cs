using Hazel.Tools;
using System;
using System.Collections.Concurrent;
using System.Threading;


namespace Hazel.Udp
{
    partial class UdpConnection
    {
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

        // TODO: Technically, Min(MissingPingsUntilDisconnect + 1, 16) would be better, but I don't want to mess with it.
        // The real point is that we're bounding the number of active pings.
        private PingBuffer activePings = new PingBuffer(16);

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

            byte[] bytes = new byte[3];
            bytes[0] = (byte)UdpSendOption.Ping;
            bytes[1] = (byte)(id >> 8);
            bytes[2] = (byte)id;

            // TODO: This could overwrite a date, perhaps we should track pings that are simply never ack'd?
            this.activePings.AddPing(id);
            
            WriteBytesToConnection(bytes, bytes.Length);

            Statistics.LogReliableSend(0);
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
            this.keepAliveTimer?.Dispose();
        }
    }
}