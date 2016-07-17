using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Event arguments for the <see cref="Connection.Disconnected"/> event.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This contains information about the cause of a disconnection and is passed to subscribers of the
    ///         <see cref="Connection.Disconnected"/> event.
    ///     </para>
    ///     <include file="DocInclude/common.xml" path="docs/item[@name='Recyclable']/*" />
    /// </remarks>
    /// <threadsafety static="true" instance="true"/>
    public class DisconnectedEventArgs : EventArgs, IRecyclable
    {
        /// <summary>
        ///     Object pool for this event.
        /// </summary>
        static readonly ObjectPool<DisconnectedEventArgs> objectPool = new ObjectPool<DisconnectedEventArgs>(() => new DisconnectedEventArgs());

        /// <summary>
        ///     Returns an instance of this object from the pool.
        /// </summary>
        /// <returns>A new or recycled DisconnectedEventArgs object.</returns>
        internal static DisconnectedEventArgs GetObject()
        {
            return objectPool.GetObject();
        }

        /// <summary>
        ///     The exception, if any, that caused the disconnect.
        /// </summary>
        /// <remarks>
        ///     If the disconnection was caused because of an exception occuring (for exemple a 
        ///     <see cref="System.Net.Sockets.SocketException"/> on network based connections) this will contain the error 
        ///     that caused it or a <see cref="HazelException"/> with the details of the exception, if the disconnection 
        ///     wasn't caused by an error then this will contain null.
        /// </remarks>
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

        /// <inheritdoc />
        public void Recycle()
        {
            objectPool.PutObject(this);
        }
    }
}
