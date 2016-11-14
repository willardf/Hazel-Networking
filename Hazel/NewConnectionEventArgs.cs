using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Event arguments for the <see cref="ConnectionListener.NewConnection"/> event.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This contains the new connection for the client that connection and is passed to subscribers of the
    ///         <see cref="ConnectionListener.NewConnection"/> event.
    ///     </para>
    ///     <include file="DocInclude/common.xml" path="docs/item[@name='Recyclable']/*" />
    /// </remarks>
    /// <threadsafety static="true" instance="true"/>
    public class NewConnectionEventArgs : EventArgs, IRecyclable
    {
        /// <summary>
        ///     Object pool for this event.
        /// </summary>
        static readonly ObjectPool<NewConnectionEventArgs> objectPool = new ObjectPool<NewConnectionEventArgs>(() => new NewConnectionEventArgs());

        /// <summary>
        ///     Returns an instance of this object from the pool.
        /// </summary>
        /// <returns>A new or recycled NewConnectionEventArgs object.</returns>
        internal static NewConnectionEventArgs GetObject()
        {
            return objectPool.GetObject();
        }

        /// <summary>
        ///     The data received from the client in the handshake.
        /// </summary>
        public byte[] HandshakeData { get; private set; }

        /// <summary>
        ///     The <see cref="Connection"/> to the new client.
        /// </summary>
        public Connection Connection { get; private set; }

        /// <summary>
        ///     Private constructor for object pool.
        /// </summary>
        NewConnectionEventArgs()
        {

        }

        /// <summary>
        ///     Sets the members of the arguments.
        /// </summary>
        /// <param name="bytes">The bytes that were received in the handshake.</param>
        /// <param name="connection">The new connection</param>
        internal void Set(byte[] bytes, Connection connection)
        {
            this.HandshakeData = bytes;
            this.Connection = connection;
        }

        /// <inheritdoc />
        public void Recycle()
        {
            objectPool.PutObject(this);
        }
    }
}
