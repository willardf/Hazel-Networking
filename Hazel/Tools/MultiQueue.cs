using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace Hazel.Tools
{
    public class MultiQueue<T>
    {
        private int[] maxQueueLengths;

        private ConcurrentDictionary<T, int> itemToSet = new ConcurrentDictionary<T, int>();

        private BlockingCollection<T>[] sets;

        public int Count => this.sets.Sum(s => s.Count);

        public MultiQueue(int numQueues)
        {
            this.maxQueueLengths = new int[numQueues];
            this.itemToSet = new ConcurrentDictionary<T, int>();
            this.sets = new BlockingCollection<T>[numQueues];
            for (int i = 0; i < this.sets.Length; i++)
            {
                this.sets[i] = new BlockingCollection<T>();
            }
        }

        public bool TryAdd(T item)
        {
        tryagain:
            if (!this.itemToSet.TryGetValue(item, out int setIdx))
            {
                setIdx = 0;
                int setLen = this.maxQueueLengths[0];
                for (int i = 0; i < this.maxQueueLengths.Length; ++i)
                {
                    int newSetLen = this.maxQueueLengths[i];
                    if (newSetLen < setLen)
                    {
                        setIdx = i;
                        setLen = newSetLen;
                    }
                }

                if (this.itemToSet.TryAdd(item, setIdx))
                {
                    Interlocked.Increment(ref this.maxQueueLengths[setIdx]);
                } 
                else
                {
                    goto tryagain;
                }
            }

            try
            {
                var set = this.sets[setIdx];
                return set.TryAdd(item);
            }
            catch
            {
                return false;
            }
        }

        public bool TryTake(int queueId, out T item)
        {
            if (queueId >= this.sets.Length)
            {
                throw new ArgumentOutOfRangeException("QueueId >= NumQueues");
            }

            return this.sets[queueId].TryTake(out item, Timeout.Infinite);
        }

        public void CompleteAdding()
        {
            foreach (var set in this.sets)
            {
                set.CompleteAdding();
            }
        }
    }
}
