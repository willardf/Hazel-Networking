using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hazel
{
    /// <summary>
    ///     UdpConnection part which handles keepalive packets.
    /// </summary>
    partial class UdpConnection
    {
        /// <summary>
        ///     The interval from data being received or transmitted to a keepalive packet being sent.
        /// </summary>
        /// <remarks>
        ///     Set to System.Threading.Timeout.Infinite to disable keepalive packets.
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
        int keepAliveInterval = 10000;

        /// <summary>
        ///     The timer creating keepalive pulses.
        /// </summary>
        Timer keepAliveTimer;

        /// <summary>
        ///     Lock for keep alive timer.
        /// </summary>
        Object keepAliveTimerLock = new Object();

        /// <summary>
        ///     Starts the keepalive timer.
        /// </summary>
        void InitializeKeepAliveTimer()
        {
            lock (keepAliveTimerLock)
            {
                keepAliveTimer = new Timer(
                    (o) =>
                    {
                        Trace.WriteLine("Keepalive packet sent.");
                        SendHello(null);
                    },
                    null,
                    keepAliveInterval,
                    keepAliveInterval
                );
            }
        }

        /// <summary>
        ///     Resets the keepalive timer to zero.
        /// </summary>
        void ResetKeepAliveTimer()
        {
            lock (keepAliveTimerLock)
                keepAliveTimer.Change(keepAliveInterval, keepAliveInterval);
        }

        /// <summary>
        ///     Disposes of the keep alive timer.
        /// </summary>
        void DisposeKeepAliveTimer()
        {
            lock(keepAliveTimerLock)
                keepAliveTimer.Dispose();
        }
    }
}
