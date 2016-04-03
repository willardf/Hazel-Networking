using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Events args for disconnected events.
    /// </summary>
    public class DisconnectedEventArgs
    {
        /// <summary>
        ///     The exception, if any, that caused the disconnect, otherwise null.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        ///     Creates a DisconnectedEventArgs from the given exception or null
        /// </summary>
        /// <param name="e">The exception if the cause.</param>
        internal DisconnectedEventArgs(Exception e)
        {
            this.Exception = e;
        }
    }
}
