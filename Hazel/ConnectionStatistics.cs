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
        private const int ExpectedMTU = 1200;

        /// <summary>
        ///     The total number of messages sent.
        /// </summary>
        public int MessagesSent
        {
            get
            {
                return UnreliableMessagesSent + ReliableMessagesSent + FragmentedMessagesSent + AcknowledgementMessagesSent + HelloMessagesSent;
            }
        }

        private int packetsSent;
        public int PacketsSent => this.packetsSent;

        private int reliablePacketsAcknowledged;
        public int ReliablePacketsAcknowledged => this.reliablePacketsAcknowledged;

        /// <summary>
        ///     The number of messages sent larger than 576 bytes. This is smaller than most default MTUs.
        /// </summary>
        /// <remarks>
        ///     This is the number of unreliable messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogUnreliableSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int FragmentableMessagesSent
        {
            get
            {
                return fragmentableMessagesSent;
            }
        }

        /// <summary>
        ///     The number of messages sent larger than 576 bytes.
        /// </summary>
        int fragmentableMessagesSent;

        /// <summary>
        ///     The number of unreliable messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of unreliable messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogUnreliableSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int UnreliableMessagesSent
        {
            get
            {
                return unreliableMessagesSent;
            }
        }

        /// <summary>
        ///     The number of unreliable messages sent.
        /// </summary>
        int unreliableMessagesSent;

        /// <summary>
        ///     The number of reliable messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of reliable messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogReliableSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int ReliableMessagesSent => reliableMessagesSent;
        int reliableMessagesSent;

        /// <summary>
        ///     The number of fragmented messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of fragmented messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogFragmentedSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int FragmentedMessagesSent => fragmentedMessagesSent;
        int fragmentedMessagesSent;

        /// <summary>
        ///     The number of acknowledgement messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of acknowledgements that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogAcknowledgementSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int AcknowledgementMessagesSent => acknowledgementMessagesSent;
        int acknowledgementMessagesSent;

        /// <summary>
        ///     The number of hello messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of hello messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogHelloSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int HelloMessagesSent => helloMessagesSent;
        int helloMessagesSent;

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
        public long DataBytesSent => Interlocked.Read(ref dataBytesSent);
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
        public long TotalBytesSent => Interlocked.Read(ref totalBytesSent);
        long totalBytesSent;

        /// <summary>
        ///     The total number of messages received.
        /// </summary>
        public int MessagesReceived
        {
            get => UnreliableMessagesReceived
                + ReliableMessagesReceived 
                + FragmentedMessagesReceived 
                + AcknowledgementMessagesReceived 
                + HelloMessagesReceived
                + PingMessagesReceived;
        }
        
        /// <summary>
        ///     The number of unreliable messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of unreliable messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogUnreliableReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int UnreliableMessagesReceived => unreliableMessagesReceived;
        int unreliableMessagesReceived;

        /// <summary>
        ///     The number of reliable messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of reliable messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogReliableReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int ReliableMessagesReceived => reliableMessagesReceived;
        int reliableMessagesReceived;

        /// <summary>
        ///     The number of fragmented messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of fragmented messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogFragmentedReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int FragmentedMessagesReceived => fragmentedMessagesReceived;
        int fragmentedMessagesReceived;

        /// <summary>
        ///     The number of acknowledgement messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of acknowledgement messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogAcknowledgemntReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int AcknowledgementMessagesReceived => acknowledgementMessagesReceived;
        int acknowledgementMessagesReceived;

        /// <summary>
        ///     The number of ping messages received.
        /// </summary>
        public int PingMessagesReceived => pingMessagesReceived;
        private int pingMessagesReceived;


        /// <summary>
        ///     The number of ping messages sent.
        /// </summary>
        public int PingMessagesSent => pingMessagesSent;
        private int pingMessagesSent;

        /// <summary>
        ///     The number of hello messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of hello messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogHelloReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int HelloMessagesReceived => helloMessagesReceived;
        int helloMessagesReceived;

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
        public long DataBytesReceived => Interlocked.Read(ref dataBytesReceived);
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
        public long TotalBytesReceived => Interlocked.Read(ref totalBytesReceived);
        long totalBytesReceived;

        /// <summary>
        /// Number of reliable messages resent
        /// </summary>
        public int MessagesResent => messagesResent;
        int messagesResent;

        /// <summary>
        ///     Logs the sending of an unreliable data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        internal void LogUnreliableSend(int dataLength)
        {
            Interlocked.Increment(ref unreliableMessagesSent);
            Interlocked.Add(ref dataBytesSent, dataLength);
            
        }

        /// <param name="totalLength">The total number of bytes sent.</param>
        internal void LogPacketSend(int totalLength)
        {
            Interlocked.Increment(ref this.packetsSent);
            Interlocked.Add(ref totalBytesSent, totalLength);

            if (totalLength > ExpectedMTU)
            {
                Interlocked.Increment(ref fragmentableMessagesSent);
            }
        }

        /// <summary>
        ///     Logs the sending of a reliable data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        internal void LogReliableSend(int dataLength)
        {
            Interlocked.Increment(ref reliableMessagesSent);
            Interlocked.Add(ref dataBytesSent, dataLength);
        }

        /// <summary>
        ///     Logs the sending of a fragmented data packet in the statistics.
        /// </summary>
        /// <param name="dataLength">The number of bytes of data sent.</param>
        /// <param name="totalLength">The total number of bytes sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        internal void LogFragmentedSend(int dataLength)
        {
            Interlocked.Increment(ref fragmentedMessagesSent);
            Interlocked.Add(ref dataBytesSent, dataLength);
        }

        /// <summary>
        ///     Logs the sending of a acknowledgement data packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        internal void LogAcknowledgementSend()
        {
            Interlocked.Increment(ref acknowledgementMessagesSent);
        }

        /// <summary>
        ///     Logs the sending of a hellp data packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        internal void LogHelloSend()
        {
            Interlocked.Increment(ref helloMessagesSent);
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
        ///     Logs the unique acknowledgement of a ping or reliable data packet.
        /// </summary>
        internal void LogReliablePacketAcknowledged()
        {
            Interlocked.Increment(ref this.reliablePacketsAcknowledged);
        }

        /// <summary>
        ///     Logs the sending of a ping packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes received.</param>
        /// <remarks>
        ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
        /// </remarks>
        internal void LogPingSend(int totalLength)
        {
            Interlocked.Increment(ref pingMessagesSent);
            // Interlocked.Add(ref totalBytesSent, totalLength); // NOTE: bytes sent is already added to for this via LogPacketSend
        }

        /// <summary>
        ///     Logs the receiving of a hello data packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes received.</param>
        /// <remarks>
        ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
        /// </remarks>
        internal void LogPingReceive(int totalLength)
        {
            Interlocked.Increment(ref pingMessagesReceived);
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
            Interlocked.Increment(ref reliableMessagesReceived);
            Interlocked.Add(ref totalBytesReceived, totalLength);
        }

        internal void LogMessageResent()
        {
            Interlocked.Increment(ref messagesResent);
        }
    }
}
