using System;
#if NET_45
using System.Collections.Concurrent;
#else
using System.Collections.Generic;
#endif
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     A fairly simple object pool for items that will be created a lot.
    /// </summary>
    /// <typeparam name="T">The type that is pooled.</typeparam>
    /// <threadsafety static="true" instance="true"/>
    sealed class ObjectPool<T> where T : IRecyclable
    {
        /// <summary>
        ///     Our pool of objects
        /// </summary>
#if NET_45
        ConcurrentBag<T> pool = new ConcurrentBag<T>();
#else
        Queue<T> pool = new Queue<T>();
#endif

        /// <summary>
        ///     The generator for creating new objects.
        /// </summary>
        /// <returns></returns>
        Func<T> objectFactory;

        /// <summary>
        ///     Internal constructor for our ObjectPool.
        /// </summary>
        internal ObjectPool(Func<T> objectFactory)
        {
            this.objectFactory = objectFactory;
        }

        /// <summary>
        ///     Returns a pooled object of type T, if none are available another is created.
        /// </summary>
        /// <returns>An instance of T.</returns>
        internal T GetObject()
        {
#if NET_45
            T item;
            if (pool.TryTake(out item))
                return item;
#else
            lock (pool)
            {
                if (pool.Count > 0)
                    return pool.Dequeue();
            }
#endif
            return objectFactory.Invoke();
        }

        /// <summary>
        ///     Returns an object to the pool.
        /// </summary>
        /// <param name="item">The item to return.</param>
        internal void PutObject(T item)
        {
#if NET_45
            pool.Add(item);
#else
            lock (pool)
                pool.Enqueue(item);
#endif
        }
    }
}
