using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace Hazel.Tools
{
    public class MultiQueue<T>
    {
        private ConcurrentDictionary<T, byte>[] sets;
        private bool addingComplete = false;

        public int Count => this.sets.Sum(s => s.Count);

        public MultiQueue(int numQueues)
        {
            this.sets = new ConcurrentDictionary<T, byte>[numQueues];
            for (int i = 0; i < this.sets.Length; i++)
            {
                this.sets[i] = new ConcurrentDictionary<T, byte>();
            }
        }

        public bool TryAdd(T item)
        {
            if (this.addingComplete)
            {
                return false;
            }
            
            long setIdx = (uint)item.GetHashCode() % this.sets.Length;
            var set = this.sets[setIdx];
            
            if (set.TryAdd(item, 0))
            {
                Monitor.Enter(set);
                Monitor.Pulse(set);
                Monitor.Exit(set);
            }

            return true;
        }

        public bool TryTake(int queueId, out T item)
        {
            if (queueId >= this.sets.Length)
            {
                throw new ArgumentOutOfRangeException("QueueId >= NumQueues");
            }

            var set = this.sets[queueId];

        tryAgain:
            foreach(var key in set.Keys)
            {
                if (set.TryRemove(key, out _))
                {                    
                    item = key;
                    return true;
                }
            }

            if (!this.addingComplete)
            {
                Monitor.Enter(set);
                Monitor.Wait(set);
                Monitor.Exit(set);
                goto tryAgain;
            }

            item = default;
            return false;
        }

        public void CompleteAdding()
        {
            this.addingComplete = true;
            foreach (var set in this.sets)
            {
                Monitor.Enter(set);
                Monitor.PulseAll(set);
                Monitor.Exit(set);
            }
        }
    }
}
