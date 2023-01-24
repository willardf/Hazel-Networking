using System.Threading;

namespace Hazel
{
    public class ListenerStatistics
    {
        private int _receiveThreadBlocked;
        public int ReceiveThreadBlocked => this._receiveThreadBlocked;

        private long _bytesSent;
        public long BytesSent => this._bytesSent;

        internal void AddReceiveThreadBlocking()
        {
            Interlocked.Increment(ref _receiveThreadBlocked);
        }

        internal void AddBytesSent(long bytes)
        {
            Interlocked.Add(ref _bytesSent, bytes);
        }
    }
}
