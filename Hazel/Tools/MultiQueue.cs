using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Hazel.Tools
{
    public class MultiQueue<T>
    {
        private ConcurrentQueue<T>[] sets;
        private SemaphoreSlim[] locks;

        private bool addingComplete;

        public int Count => this.sets.Sum(s => s.Count);

        public MultiQueue(int numQueues)
        {
            this.sets = new ConcurrentQueue<T>[numQueues];
            this.locks = new SemaphoreSlim[numQueues];

            for (int i = 0; i < this.sets.Length; i++)
            {
                this.sets[i] = new ConcurrentQueue<T>();
                this.locks[i] = new SemaphoreSlim(0);
            }
        }

        public bool TryAdd(T item)
        {
            if (this.addingComplete) return false;

            int setIdx = item.GetHashCode() % this.sets.Length;

            try
            {
                this.sets[setIdx].Enqueue(item);
                this.locks[setIdx].Release();
                return true;
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

            this.locks[queueId].Wait();
            var set = this.sets[queueId];
            if (set.Count > 0)
            {
                return set.TryDequeue(out item);
            }

            item = default;
            return false;
        }

        public void CompleteAdding()
        {
            this.addingComplete = true;
            foreach (var l in this.locks)
            {
                l.Release();
            }
        }

        internal IEnumerable<T> GetConsumingEnumerable(int queueId)
        {
            while (this.TryTake(queueId, out var item))
            {
                yield return item;
            }
        }

        internal void Dispose()
        {
            foreach(var l in this.locks)
            {
                l.Dispose();
            }
        }
    }
}
