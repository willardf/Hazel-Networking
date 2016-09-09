using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Event arguments for the <see cref="Connection.DataReceived"/> event.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This contains information about messages received by a connection and is passed to subscribers of the 
    ///         <see cref="Connection.DataReceived">DataEvent</see>. 
    ///     </para>
    ///     <include file="DocInclude/common.xml" path="docs/item[@name='Recyclable']/*" />
    /// </remarks>
    /// <threadsafety static="true" instance="true"/>
    public class DataReceivedEventArgs : EventArgs, IRecyclable
    {
        /// <summary>
        ///     Object pool for this event.
        /// </summary>
        static readonly ObjectPool<DataReceivedEventArgs> objectPool = new ObjectPool<DataReceivedEventArgs>(() => new DataReceivedEventArgs());

        /// <summary>
        ///     Returns an instance of this object from the pool.
        /// </summary>
        /// <returns>A new or recycled DataEventArgs object.</returns>
        internal static DataReceivedEventArgs GetObject()
        {
            return objectPool.GetObject();
        }

        /// <summary>
        ///     The bytes received from the client.
        /// </summary>
        public byte[] Bytes { get; private set; }

        /// <summary>
        ///     The <see cref="SendOption"/> the data was sent with.
        /// </summary>
        public SendOption SendOption { get; private set; }

        /// <summary>
        ///     Private constructor for object pool.
        /// </summary>
        DataReceivedEventArgs()
        {

        }

        /// <summary>
        ///     Sets the members of the arguments.
        /// </summary>
        /// <param name="bytes">The bytes received.</param>
        /// <param name="sendOption">The send option used to send the data.</param>
        internal void Set(byte[] bytes, SendOption sendOption)
        {
            this.Bytes = bytes;
            this.SendOption = sendOption;
        }

        /// <inheritdoc />
        public void Recycle()
        {
            objectPool.PutObject(this);
        }
    }
}
