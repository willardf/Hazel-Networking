using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel
{
    /// <summary>
    ///     A fairly simple object pool for items that will be created a lot.
    /// </summary>
    /// <typeparam name="T">The type that is pooled.</typeparam>
    class ObjectPool<T> where T : IRecyclable
    {
        /// <summary>
        ///     Our pool of objects
        /// </summary>
        ConcurrentBag<T> pool = new ConcurrentBag<T>();

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
            T item;
            if (pool.TryTake(out item))
                return item;

            return objectFactory.Invoke();
        }

        /// <summary>
        ///     Returns an object to the pool.
        /// </summary>
        /// <param name="item">The item to return.</param>
        internal void PutObject(T item)
        {
            pool.Add(item);
        }
    }
}
