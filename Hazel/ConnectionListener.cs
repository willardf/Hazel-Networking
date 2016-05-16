using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Hazel
{
    /// <summary>
    ///     Base class for all connection listeners.
    /// </summary>
    public abstract class ConnectionListener : IDisposable
    {
        /// <summary>
        ///     Invoked when a new TCP connection is heard.
        /// </summary>
        public event EventHandler<NewConnectionEventArgs> NewConnection;

        /// <summary>
        ///     Makes this connection listener begin listening for connections.
        /// </summary>
        public abstract void Start();

        /// <summary>
        ///     Invokes the NewConnection event with the supplied args.
        /// </summary>
        /// <param name="args">The arguments for the event.</param>
        protected void InvokeNewConnection(Connection connection)
        {
            //Get new args
            NewConnectionEventArgs args = NewConnectionEventArgs.GetObject();
            args.Set(connection);

            //Make a copy to avoid race condition between null check and invocation
            EventHandler<NewConnectionEventArgs> handler = NewConnection;
            if (handler != null)
                handler(this, args);
        }

        /// <summary>
        ///     Call to dispose of the connection listener.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        ///     Called when the object is being disposed.
        /// </summary>
        /// <param name="disposing">Are we disposing?</param>
        protected virtual void Dispose(bool disposing)
        {

        }
    }
}
