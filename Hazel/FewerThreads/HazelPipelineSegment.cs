using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Hazel
{
    public class HazelPipelineSegment<InputType> : IDisposable
    {
        private readonly HazelThreadPool threads;
        private readonly BlockingCollection<InputType> inputs = new BlockingCollection<InputType>();
        private readonly Action<InputType> factory;

        public int Count => this.inputs.Count;

        public HazelPipelineSegment(int numThreads, Action<InputType> factory)
        {
            this.threads = new HazelThreadPool(numThreads, RunProcessing);
            this.factory = factory;
        }

        private void RunProcessing()
        {
            while (this.inputs.TryTake(out var item, Timeout.Infinite))
            {
                this.factory(item);
            }
        }

        public void Start()
        {
            this.threads.Start();
        }

        public void Join()
        {
            this.inputs.CompleteAdding();
            this.threads.Join();
        }

        public void AddInput(InputType item)
        {
            this.inputs.TryAdd(item);
        }

        public void Dispose()
        {
            this.inputs.Dispose();
        }
    }
}