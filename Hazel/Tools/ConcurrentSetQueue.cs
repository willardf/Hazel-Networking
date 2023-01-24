using System.Collections.Concurrent;
using System.Threading;

namespace Hazel.Tools
{
    public class ConcurrentSetQueue<T>
    {
        private ConcurrentDictionary<T, byte> set = new ConcurrentDictionary<T, byte>();
        private bool addingComplete = false;

        public int Count => this.set.Count;

        private object SyncRoot => this.set;

        public bool TryAdd(T item)
        {
            if (this.addingComplete)
            {
                return false;
            }

            if (this.set.TryAdd(item, 0))
            {
                Monitor.Enter(this.SyncRoot);
                Monitor.Pulse(this.SyncRoot);
                Monitor.Exit(this.SyncRoot);
            }

            return true;
        }

        public bool TryRemove(T item)
        {
            return this.set.TryRemove(item, out _);
        }

        public bool TryTake(out T item)
        {
        tryAgain:
            foreach(var key in this.set.Keys)
            {
                if (this.set.TryRemove(key, out _))
                {                    
                    item = key;
                    return true;
                }
            }

            if (!this.addingComplete)
            {
                Monitor.Enter(this.SyncRoot);
                Monitor.Wait(this.SyncRoot);
                Monitor.Exit(this.SyncRoot);
                goto tryAgain;
            }

            item = default;
            return false;
        }

        public void CompleteAdding()
        {
            this.addingComplete = true;
            Monitor.Enter(this.SyncRoot);
            Monitor.PulseAll(this.SyncRoot);
            Monitor.Exit(this.SyncRoot);
        }
    }
}
