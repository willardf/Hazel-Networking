using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel
{
    /// <summary>
    ///     Represents the state of the current receive operation for TCP connections.
    /// </summary>
    struct StateObject
    {
        /// <summary>
        ///     The buffer we're receiving.
        /// </summary>
        internal byte[] buffer;

        /// <summary>
        ///     The total number of bytes received so far.
        /// </summary>
        internal int totalBytesReceived;

        /// <summary>
        ///     The callback to invoke once the buffer has been filled.
        /// </summary>
        internal Action<byte[]> callback;

        /// <summary>
        ///     Creates a StateObject with the specified length.
        /// </summary>
        /// <param name="length">The number of bytes expected to be received.</param>
        internal StateObject(int length, Action<byte[]> callback)
        {
            this.buffer = new byte[length];
            this.totalBytesReceived = 0;
            this.callback = callback;
        }
    }
}
