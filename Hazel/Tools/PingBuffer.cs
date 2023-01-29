using System;

namespace Hazel.Tools
{
    public class PingBuffer
    {
        private const ushort InvalidatingFactor = ushort.MaxValue / 2;

        private struct PingInfo
        {
            public ushort Id;
            public DateTime SentAt;
        }

        private PingInfo[] activePings;
        private int head; // The location of the next usable activePing

        public PingBuffer(int maxPings)
        {
            this.activePings = new PingInfo[maxPings];

            // We don't want the first few packets to match id before we set anything.
            for (int i = 0; i < this.activePings.Length; ++i)
            {
                this.activePings[i].Id = InvalidatingFactor;
            }
        }

        public void AddPing(ushort id)
        {
            lock (this.activePings)
            {
                this.activePings[this.head].Id = id;
                this.activePings[this.head].SentAt = DateTime.UtcNow;
                this.head++;
                if (this.head >= this.activePings.Length)
                {
                    this.head = 0;
                }
            }
        }

        public bool TryFindPing(ushort id, out DateTime sentAt)
        {
            lock (this.activePings)
            {
                for (int i = 0; i < this.activePings.Length; ++i)
                {
                    if (this.activePings[i].Id == id)
                    {
                        sentAt = this.activePings[i].SentAt;
                        this.activePings[i].Id += InvalidatingFactor;
                        return true;
                    }
                }
            }

            sentAt = default;
            return false;
        }
    }
}
