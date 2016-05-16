using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Event args for new connection events.
    /// </summary>
    public class NewConnectionEventArgs : EventArgs, IRecyclable
    {
        /// <summary>
        ///     Object pool for this event.
        /// </summary>
        static readonly ObjectPool<NewConnectionEventArgs> objectPool = new ObjectPool<NewConnectionEventArgs>(() => new NewConnectionEventArgs());

        /// <summary>
        ///     Returns an instance of this object from the pool.
        /// </summary>
        /// <returns></returns>
        internal static NewConnectionEventArgs GetObject()
        {
            return objectPool.GetObject();
        }

        /// <summary>
        ///     The new connection.
        /// </summary>
        public Connection Connection { get; private set; }

        /// <summary>
        ///     Private constructor for thread pool.
        /// </summary>
        NewConnectionEventArgs()
        {

        }

        /// <summary>
        ///     Sets the members of the arguments.
        /// </summary>
        /// <param name="Connection"></param>
        internal void Set(Connection Connection)
        {
            this.Connection = Connection;
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
