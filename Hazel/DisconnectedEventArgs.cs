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
    public class DisconnectedEventArgs : EventArgs
    {
        /// <summary>
        ///     The exception, if any, that caused the disconnect.
        /// </summary>
        /// <remarks>
        ///     If the disconnection was caused because of an exception occuring (for exemple a 
        ///     <see cref="System.Net.Sockets.SocketException"/> on network based connections) this will contain the error 
        ///     that caused it or a <see cref="HazelException"/> with the details of the exception, if the disconnection 
        ///     wasn't caused by an error then this will contain null.
        /// </remarks>
        public readonly string Reason;

        public readonly MessageReader Message;

        public DisconnectedEventArgs(string reason, MessageReader message)
        {
            this.Reason = reason;
            this.Message = message;
        }
    }
}
