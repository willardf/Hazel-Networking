using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Event args for new connection events.
    /// </summary>
    public class NewConnectionEventArgs : EventArgs
    {
        /// <summary>
        ///     The new connection.
        /// </summary>
        public Connection Connection { get; private set; }

        internal NewConnectionEventArgs(Connection Connection)
        {
            this.Connection = Connection;
        }
    }
}
