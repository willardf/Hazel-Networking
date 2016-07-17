using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;


namespace Hazel
{
    /// <summary>
    ///     Holds statistics about the traffic through a <see cref="Connection"/>.
    /// </summary>
    /// <threadsafety static="true" instance="true"/>
    public class ConnectionStatistics
    {
        /// <summary>
        ///     The number of messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of messages that were sent from the <see cref="Connection"/>, incremented each time that 
        ///     LogSend is called by the Connection. Messages that caused an error are not counted and messages are only 
        ///     counted once all other operations in the send are complete.
        /// </remarks>
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
        /// <remarks>
        ///     <para>
        ///         This is the number of bytes of data (i.e. user bytes) that were sent from the <see cref="Connection"/>, 
        ///         accumulated each time that LogSend is called by the Connection. Messages that caused an error are not 
        ///         counted and messages are only counted once all other operations in the send are complete.
        ///     </para>
        ///     <para>
        ///         For the number of bytes including protocol bytes see <see cref="TotalBytesSent"/>.
        ///     </para>
        /// </remarks>
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
        /// <remarks>
        ///     <para>
        ///         This is the total number of bytes (the data bytes plus protocol bytes) that were sent from the 
        ///         <see cref="Connection"/>, accumulated each time that LogSend is called by the Connection. Messages that 
        ///         caused an error are not counted and messages are only counted once all other operations in the send are 
        ///         complete.
        ///     </para>
        ///     <para>
        ///         For the number of data bytes excluding protocol bytes see <see cref="DataBytesSent"/>.
        ///     </para>
        /// </remarks>
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
        /// <remarks>
        ///     This is the number of messages that were received by the <see cref="Connection"/>, incremented each time that 
        ///     LogReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
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
        /// <remarks>
        ///     <para>
        ///         This is the number of bytes of data (i.e. user bytes) that were received by the <see cref="Connection"/>, 
        ///         accumulated each time that LogReceive is called by the Connection. Messages are counted before the receive
        ///         event is invoked.
        ///     </para>
        ///     <para>
        ///         For the number of bytes including protocol bytes see <see cref="TotalBytesReceived"/>.
        ///     </para>
        /// </remarks>
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
        /// <remarks>
        ///     <para>
        ///         This is the total number of bytes (the data bytes plus protocol bytes) that were received by the 
        ///         <see cref="Connection"/>, accumulated each time that LogReceive is called by the Connection. Messages are 
        ///         counted before the receive event is invoked.
        ///     </para>
        ///     <para>
        ///         For the number of data bytes excluding protocol bytes see <see cref="DataBytesReceived"/>.
        ///     </para>
        /// </remarks>
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
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
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
        /// <remarks>
        ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
        /// </remarks>
        internal void LogReceive(int dataLength, int totalLength)
        {
            Interlocked.Increment(ref messagesReceived);
            Interlocked.Add(ref dataBytesReceived, dataLength);
            Interlocked.Add(ref totalBytesReceived, totalLength);
        }
    }
}
