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
            foreach (var item in this.inputs.GetConsumingEnumerable())
            {
                try
                {
                    this.factory(item);
                }
                catch
                {
                }
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