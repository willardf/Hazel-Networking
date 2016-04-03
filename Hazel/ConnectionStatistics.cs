using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hazel
{
    /// <summary>
    ///     Holds statistics about the traffic through a Connection.
    /// </summary>
    public class ConnectionStatistics
    {
        /// <summary>
        ///     The number of messages sent.
        /// </summary>
        public long MessagesSent
        {
            get
            {
                return Interlocked.Read(ref messagesSent);
            }
        }

        /// <summary>
        ///     The number of messages sent.
        /// </summary>
        long messagesSent;

        /// <summary>
        ///     The number of bytes of data sent.
        /// </summary>
        public long DataBytesSent
        {
            get
            {
                return Interlocked.Read(ref dataBytesSent);
            }
        }

        /// <summary>
        ///     The number of bytes of data sent.
        /// </summary>
        long dataBytesSent;

        /// <summary>
        ///     The number of bytes sent in total.
        /// </summary>
        public long TotalBytesSent
        {
            get
            {
                return Interlocked.Read(ref totalBytesSent);
            }
        }

        /// <summary>
        ///     The number of bytes sent in total.
        /// </summary>
        long totalBytesSent;

        /// <summary>
        ///     The number of messages received.
        /// </summary>
        public long MessagesReceived
        {
            get
            {
                return Interlocked.Read(ref messagesReceived);
            }
        }

        /// <summary>
        ///     The number of messages received.
        /// </summary>
        long messagesReceived;

        /// <summary>
        ///     The number of bytes of data received.
        /// </summary>
        public long DataBytesReceived
        {
            get
            {
                return Interlocked.Read(ref dataBytesReceived);
            }
        }

        /// <summary>
        ///     The number of bytes of data received.
        /// </summary>
        long dataBytesReceived;

        /// <summary>
        ///     The number of bytes received in total.
        /// </summary>
        public long TotalBytesReceived
        {
            get
            {
                return Interlocked.Read(ref totalBytesReceived);
            }
        }

        /// <summary>
        ///     The number of bytes received in total.
        /// </summary>
        long totalBytesReceived;

        /// <summary>
        ///     Logs the sending of a data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data sent.</param>
        /// <param name="totalLength">The total number of bytes sent.</param>
        internal void LogSend(int dataLength, int totalLength)
        {
            Interlocked.Increment(ref messagesSent);
            Interlocked.Add(ref dataBytesSent, dataLength);
            Interlocked.Add(ref totalBytesSent, totalLength);
        }

        /// <summary>
        ///     Logs the receiving of a data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data received.</param>
        /// <param name="totalLength">The total number of bytes received.</param>
        internal void LogReceive(int dataLength, int totalLength)
        {
            Interlocked.Increment(ref messagesReceived);
            Interlocked.Add(ref dataBytesReceived, dataLength);
            Interlocked.Add(ref totalBytesReceived, totalLength);
        }
    }
}
