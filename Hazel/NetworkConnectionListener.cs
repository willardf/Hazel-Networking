using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Hazel
{
    /// <summary>
    ///     Abstract base class for a <see cref="ConnectionListener"/> for network based connections.
    /// </summary>
    public abstract class NetworkConnectionListener : ConnectionListener
    {
        /// <summary>
        ///     The local IP address the listener is listening for new clients on.
        /// </summary>
        public IPAddress IPAddress { get; protected set; }

        /// <summary>
        ///     The port the listener is listening for new clients on.
        /// </summary>
        public int Port { get; protected set; }
    }
}
