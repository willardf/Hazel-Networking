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
        ///     The total number of messages sent.
        /// </summary>
        public long MessagesSent
        {
            get
            {
                return UnreliableMessagesSent + ReliableMessagesSent + FragmentedMessagesSent + AcknowledgementMessagesSent + HelloMessagesSent;
            }
        }

        /// <summary>
        ///     The number of unreliable messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of unreliable messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogUnreliableSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public long UnreliableMessagesSent
        {
            get
            {
                return Interlocked.Read(ref unreliableMessagesSent);
            }
        }

        /// <summary>
        ///     The number of unreliable messages sent.
        /// </summary>
        long unreliableMessagesSent;

        /// <summary>
        ///     The number of reliable messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of reliable messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogReliableSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public long ReliableMessagesSent
        {
            get
            {
                return Interlocked.Read(ref reliableMessagesSent);
            }
        }

        /// <summary>
        ///     The number of unreliable messages sent.
        /// </summary>
        long reliableMessagesSent;

        /// <summary>
        ///     The number of fragmented messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of fragmented messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogFragmentedSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public long FragmentedMessagesSent
        {
            get
            {
                return Interlocked.Read(ref fragmentedMessagesSent);
            }
        }

        /// <summary>
        ///     The number of fragmented messages sent.
        /// </summary>
        long fragmentedMessagesSent;

        /// <summary>
        ///     The number of acknowledgement messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of acknowledgements that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogAcknowledgementSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public long AcknowledgementMessagesSent
        {
            get
            {
                return Interlocked.Read(ref acknowledgementMessagesSent);
            }
        }

        /// <summary>
        ///     The number of acknowledgement messages sent.
        /// </summary>
        long acknowledgementMessagesSent;

        /// <summary>
        ///     The number of hello messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of hello messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogHelloSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public long HelloMessagesSent
        {
            get
            {
                return Interlocked.Read(ref helloMessagesSent);
            }
        }

        /// <summary>
        ///     The number of hello messages sent.
        /// </summary>
        long helloMessagesSent;

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
        ///     The total number of messages received.
        /// </summary>
        public long MessagesReceived
        {
            get
            {
                return UnreliableMessagesReceived + ReliableMessagesReceived + FragmentedMessagesReceived + AcknowledgementMessagesReceived + helloMessagesReceived;
            }
        }
        
        /// <summary>
        ///     The number of unreliable messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of unreliable messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogUnreliableReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public long UnreliableMessagesReceived
        {
            get
            {
                return Interlocked.Read(ref unreliableMessagesReceived);
            }
        }

        /// <summary>
        ///     The number of unreliable messages received.
        /// </summary>
        long unreliableMessagesReceived;

        /// <summary>
        ///     The number of reliable messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of reliable messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogReliableReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public long ReliableMessagesReceived
        {
            get
            {
                return Interlocked.Read(ref reliableMessagesReceived);
            }
        }

        /// <summary>
        ///     The number of reliable messages received.
        /// </summary>
        long reliableMessagesReceived;

        /// <summary>
        ///     The number of fragmented messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of fragmented messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogFragmentedReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public long FragmentedMessagesReceived
        {
            get
            {
                return Interlocked.Read(ref fragmentedMessagesReceived);
            }
        }

        /// <summary>
        ///     The number of fragmented messages received.
        /// </summary>
        long fragmentedMessagesReceived;

        /// <summary>
        ///     The number of acknowledgement messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of acknowledgement messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogAcknowledgemntReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public long AcknowledgementMessagesReceived
        {
            get
            {
                return Interlocked.Read(ref acknowledgementMessagesReceived);
            }
        }

        /// <summary>
        ///     The number of acknowledgement messages received.
        /// </summary>
        long acknowledgementMessagesReceived;

        /// <summary>
        ///     The number of hello messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of hello messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogHelloReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public long HelloMessagesReceived
        {
            get
            {
                return Interlocked.Read(ref helloMessagesReceived);
            }
        }

        /// <summary>
        ///     The number of hello messages received.
        /// </summary>
        long helloMessagesReceived;

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
        ///     Logs the sending of an unreliable data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data sent.</param>
        /// <param name="totalLength">The total number of bytes sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        internal void LogUnreliableSend(int dataLength, int totalLength)
        {
            Interlocked.Increment(ref unreliableMessagesSent);
            Interlocked.Add(ref dataBytesSent, dataLength);
            Interlocked.Add(ref totalBytesSent, totalLength);
        }

        /// <summary>
        ///     Logs the sending of a reliable data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data sent.</param>
        /// <param name="totalLength">The total number of bytes sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        internal void LogReliableSend(int dataLength, int totalLength)
        {
            Interlocked.Increment(ref reliableMessagesSent);
            Interlocked.Add(ref dataBytesSent, dataLength);
            Interlocked.Add(ref totalBytesSent, totalLength);
        }

        /// <summary>
        ///     Logs the sending of a fragmented data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data sent.</param>
        /// <param name="totalLength">The total number of bytes sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        internal void LogFragmentedSend(int dataLength, int totalLength)
        {
            Interlocked.Increment(ref fragmentedMessagesSent);
            Interlocked.Add(ref dataBytesSent, dataLength);
            Interlocked.Add(ref totalBytesSent, totalLength);
        }

        /// <summary>
        ///     Logs the sending of a acknowledgement data packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        internal void LogAcknowledgementSend(int totalLength)
        {
            Interlocked.Increment(ref acknowledgementMessagesSent);
            Interlocked.Add(ref totalBytesSent, totalLength);
        }

        /// <summary>
        ///     Logs the sending of a hellp data packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        internal void LogHelloSend(int totalLength)
        {
            Interlocked.Increment(ref helloMessagesSent);
            Interlocked.Add(ref totalBytesSent, totalLength);
        }

        /// <summary>
        ///     Logs the receiving of an unreliable data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data received.</param>
        /// <param name="totalLength">The total number of bytes received.</param>
        /// <remarks>
        ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
        /// </remarks>
        internal void LogUnreliableReceive(int dataLength, int totalLength)
        {
            Interlocked.Increment(ref unreliableMessagesReceived);
            Interlocked.Add(ref dataBytesReceived, dataLength);
            Interlocked.Add(ref totalBytesReceived, totalLength);
        }

        /// <summary>
        ///     Logs the receiving of a reliable data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data received.</param>
        /// <param name="totalLength">The total number of bytes received.</param>
        /// <remarks>
        ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
        /// </remarks>
        internal void LogReliableReceive(int dataLength, int totalLength)
        {
            Interlocked.Increment(ref reliableMessagesReceived);
            Interlocked.Add(ref dataBytesReceived, dataLength);
            Interlocked.Add(ref totalBytesReceived, totalLength);
        }

        /// <summary>
        ///     Logs the receiving of a fragmented data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data received.</param>
        /// <param name="totalLength">The total number of bytes received.</param>
        /// <remarks>
        ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
        /// </remarks>
        internal void LogFragmentedReceive(int dataLength, int totalLength)
        {
            Interlocked.Increment(ref fragmentedMessagesReceived);
            Interlocked.Add(ref dataBytesReceived, dataLength);
            Interlocked.Add(ref totalBytesReceived, totalLength);
        }

        /// <summary>
        ///     Logs the receiving of an acknowledgement data packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes received.</param>
        /// <remarks>
        ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
        /// </remarks>
        internal void LogAcknowledgementReceive(int totalLength)
        {
            Interlocked.Increment(ref acknowledgementMessagesReceived);
            Interlocked.Add(ref totalBytesReceived, totalLength);
        }

        /// <summary>
        ///     Logs the receiving of a hello data packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes received.</param>
        /// <remarks>
        ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
        /// </remarks>
        internal void LogHelloReceive(int totalLength)
        {
            Interlocked.Increment(ref helloMessagesReceived);
            Interlocked.Add(ref totalBytesReceived, totalLength);
        }
    }
}
