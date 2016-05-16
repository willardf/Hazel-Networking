using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Events args for disconnected events.
    /// </summary>
    public class DisconnectedEventArgs : IRecyclable
    {
        /// <summary>
        ///     Object pool for this event.
        /// </summary>
        static readonly ObjectPool<DisconnectedEventArgs> objectPool = new ObjectPool<DisconnectedEventArgs>(() => new DisconnectedEventArgs());

        /// <summary>
        ///     Returns an instance of this object from the pool.
        /// </summary>
        /// <returns></returns>
        internal static DisconnectedEventArgs GetObject()
        {
            return objectPool.GetObject();
        }

        /// <summary>
        ///     The exception, if any, that caused the disconnect, otherwise null.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        ///     Private constructor for object pool.
        /// </summary>
        DisconnectedEventArgs()
        {

        }

        /// <summary>
        ///     Sets the given exception for the arguments.
        /// </summary>
        /// <param name="e">The exception if the cause.</param>
        internal void Set(Exception e)
        {
            this.Exception = e;
        }

        /// <summary>
        ///     Returns this object back to the object pool.
        /// </summary>
        public void Recycle()
        {
            objectPool.PutObject(this);
        }
    }
}
