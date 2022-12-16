using System;
using System.Collections.Generic;
using System.Threading;

namespace Hazel
{
    public class CustomBlockingCollection<T> where T : struct
    {
        public int Count => this.head - this.tail;
        public bool IsFull => this.Count == this.buffer.Length;

        private T[] buffer;
        
        private volatile int tail;
        private volatile int head;

        private CancellationTokenSource addingComplete;

        private SemaphoreSlim writerLock;
        private SemaphoreSlim readerLock;

        public CustomBlockingCollection(int size)
        {
            this.addingComplete = new CancellationTokenSource();
            this.writerLock = new SemaphoreSlim(size, size);
            this.readerLock = new SemaphoreSlim(0, size);
            this.buffer = new T[size];
        }

        // Guarantee one adder!
        public bool TryAdd(T info, int wait = 0)
        {
            if (this.addingComplete.IsCancellationRequested)
            {
                return false;
            }

            if (!this.writerLock.Wait(wait, this.addingComplete.Token))
            {
                return false;
            }

            var myIdx = this.head % this.buffer.Length;

            this.buffer[myIdx] = info;
            this.head++;

            this.readerLock.Release();

            return true;
        }

        // Guarantee one adder!
        public void Add(T info)
        {
            TryAdd(info, -1);
        }

        private bool TryGet(out T item, int wait = 0)
        {
            bool hasLock = false;
            if (this.addingComplete.IsCancellationRequested)
            {
                hasLock = this.readerLock.Wait(0);
            }
            else
            {
                try
                {
                    hasLock = this.readerLock.Wait(wait, this.addingComplete.Token);
                }
                catch (OperationCanceledException)
                {
                }
            }

            if (!hasLock)
            {
                item = default;
                return false;
            }

            var myIdx = Interlocked.Increment(ref this.tail) - 1;
            myIdx %= this.buffer.Length;
            item = this.buffer[myIdx];

            this.writerLock.Release();

            return true;
        }

        public IEnumerable<T> GetConsumingEnumerable()
        {
            while (!this.addingComplete.IsCancellationRequested || this.Count > 0)
            {
                if (TryGet(out T item, -1))
                {
                    yield return item;
                }
            }
        }

        public void CompleteAdding()
        {
            this.addingComplete.Cancel();
        }

        internal void Dispose()
        {
            this.addingComplete.Dispose();
            this.writerLock.Dispose();
            this.readerLock.Dispose();
        }
    }
}