using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hazel
{
    internal class HazelThreadPool
    {
        private Thread[] threads;

        public HazelThreadPool(int numThreads, ThreadStart action)
        {
            this.threads = new Thread[numThreads];
            for (int i = 0; i < this.threads.Length; ++i)
            {
                this.threads[i] = new Thread(action);
            }
        }

        public void Start()
        {
            for (int i = 0; i < this.threads.Length; ++i)
            {
                this.threads[i].Start();
            }
        }

        public void Join()
        {
            for (int i = 0; i < this.threads.Length; ++i)
            {
                var thread = this.threads[i];
                try
                {
                    thread.Join();
                }
                catch { }
            }
        }
    }
}