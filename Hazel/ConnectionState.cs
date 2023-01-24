using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Represents the state a <see cref="Connection"/> is currently in.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        ///     The Connection has not been established yet.
        /// </summary>
        NotConnected,
        
        /// <summary>
        ///     The Connection is currently connecting to an endpoint.
        /// </summary>
        Connecting,

        /// <summary>
        ///     The Connection is connected and data can be transfered.
        /// </summary>
        Connected,

        /// <summary>
        ///     The Connection was established, but is no longer.
        /// </summary>
        Disconnected
    }
}
