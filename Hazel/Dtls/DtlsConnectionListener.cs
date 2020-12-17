using System.Net;
using Hazel.Udp.FewerThreads;

namespace Hazel.Dtls
{
    /// <summary>
    /// Listens for new UDP-DTLS connections and creates UdpConnections for them.
    /// </summary>
    /// <inheritdoc />
    public class DtlsConnectionListener : ThreadLimitedUdpConnectionListener
    {
        /// <summary>
        /// Create a new instance of the DTLS listener
        /// </summary>
        /// <param name="numWorkers"></param>
        /// <param name="endPoint"></param>
        /// <param name="logger"></param>
        /// <param name="ipMode"></param>
        public DtlsConnectionListener(int numWorkers, IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
            : base(numWorkers, endPoint, logger, ipMode)
        {
        }

        /// <inheritdoc />
        protected override void ProcessIncomingMessageFromOtherThread(MessageReader message, EndPoint peerAddress, ConnectionId connectionId)
        {
            base.ProcessIncomingMessageFromOtherThread(message, peerAddress, connectionId);
        }

        /// <inheritdoc />
        protected override void QueueRawData(ByteSpan span, EndPoint remoteEndPoint)
        {
            base.QueueRawData(span, remoteEndPoint);
        }
    }
}
