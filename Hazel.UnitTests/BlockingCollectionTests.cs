using Hazel.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hazel.UnitTests
{
    [TestClass]
    public class BlockingCollectionTests
    {
        const int TestScale = 10000000;

        [TestMethod]
        public void CanAddAndGet()
        {
            CustomBlockingCollection<int> dut = new CustomBlockingCollection<int>(10);
            for (int i = 0; i < 10; ++i)
            {
                dut.Add(i);
            }

            dut.CompleteAdding();

            int expected = 0;
            foreach (var v in dut.GetConsumingEnumerable())
            {
                Assert.AreEqual(expected++, v);
            }
        }

        [TestMethod]
        public void CanAddAndRemove()
        {
            CustomBlockingCollection<int> dut = new CustomBlockingCollection<int>(1);
            dut.Add(10);
            Assert.IsFalse(dut.TryAdd(20));
            Assert.AreEqual(10, dut.GetConsumingEnumerable().First());
        }

        [TestMethod]
        public void CanAddAndRemoveContended()
        {
            CustomBlockingCollection<int> dut = new CustomBlockingCollection<int>(10);
            Task.Run(async () =>
            {
                await Task.Delay(1);
                try
                {
                    for (int i = 0; i < TestScale; ++i)
                    {
                        dut.Add(i);
                    }
                }
                catch(Exception e)
                {
                    dut.Add(0);
                }

                dut.CompleteAdding();
            });

            int expected = 0;
            foreach (var v in dut.GetConsumingEnumerable())
            {
                Assert.AreEqual(expected++, v);
            }

            Assert.AreEqual(TestScale, expected);
        }


        [TestMethod]
        public void CanAddAndRemoveContendedTest()
        {
            BlockingCollection<int> dut = new BlockingCollection<int>(10);
            Task.Run(async () =>
            {
                await Task.Delay(1);
                for (int i = 0; i < TestScale; ++i)
                {
                    dut.Add(i);
                }

                dut.CompleteAdding();
            });

            int expected = 0;
            foreach (var v in dut.GetConsumingEnumerable())
            {
                Assert.AreEqual(expected++, v);
            }

            Assert.AreEqual(TestScale, expected);
        }


        [TestMethod]
        public void CanAddAndRemoveContendedTestMQ()
        {
            MultiQueue<int> dut = new MultiQueue<int>(1);
            Task.Run(async () =>
            {
                await Task.Delay(1);
                for (int i = 0; i < TestScale; ++i)
                {
                    dut.TryAdd(i);
                }

                dut.CompleteAdding();
            });

            int expected = 0;
            foreach (var v in dut.GetConsumingEnumerable(0))
            {
                Assert.AreEqual(expected++, v);
            }

            Assert.AreEqual(TestScale, expected);
        }

        [TestMethod]
        public void CanAddAndRemoveParallel()
        {
            CustomBlockingCollection<int> dut = new CustomBlockingCollection<int>(TestScale);

            HashSet<int> values = new HashSet<int>();

            for (int i = 0; i < TestScale; ++i)
            {
                dut.Add(i);
            }

            dut.CompleteAdding();

            Task[] tasks = new Task[Environment.ProcessorCount];
            for (int i = 0; i < tasks.Length; ++i)
            {
                tasks[i] = Task.Run(() =>
                {
                    foreach (var v in dut.GetConsumingEnumerable())
                    {
                        lock (values) values.Add(v);
                    }
                });
            }

            Task.WaitAll(tasks);
            Assert.AreEqual(TestScale, values.Count);
        }

        [TestMethod]
        public void CanAddAndRemoveParallelTest()
        {
            BlockingCollection<int> dut = new BlockingCollection<int>(TestScale);

            HashSet<int> values = new HashSet<int>();

            for (int i = 0; i < TestScale; ++i)
            {
                dut.Add(i);
            }

            dut.CompleteAdding();

            Task[] tasks = new Task[Environment.ProcessorCount];
            for (int i = 0; i < tasks.Length; ++i)
            {
                tasks[i] = Task.Run(() =>
                {
                    foreach (var v in dut.GetConsumingEnumerable())
                    {
                        lock (values) values.Add(v);
                    }
                });
            }

            Task.WaitAll(tasks);
            Assert.AreEqual(TestScale, values.Count);
        }


        [TestMethod]
        public void CanAddAndRemoveParallelMQ()
        {
            MultiQueue<int> dut = new MultiQueue<int>(Environment.ProcessorCount);

            HashSet<int> values = new HashSet<int>();

            for (int i = 0; i < TestScale; ++i)
            {
                dut.TryAdd(i);
            }

            dut.CompleteAdding();

            Task[] tasks = new Task[Environment.ProcessorCount];
            for (int i = 0; i < tasks.Length; ++i)
            {
                int tid = i;
                tasks[i] = Task.Run(() =>
                {
                    foreach (var v in dut.GetConsumingEnumerable(tid))
                    {
                        lock (values) values.Add(v);
                    }
                });
            }

            Task.WaitAll(tasks);
            Assert.AreEqual(TestScale, values.Count);
        }
    }
}
