using System;

namespace Hazel
{
    /// <summary>
    ///     Base class for all connection listeners.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         ConnectionListeners are server side objects that listen for clients and create matching server side connections 
    ///         for each client in a similar way to TCP does. These connections should already have a 
    ///         <see cref="Connection.State">State</see> of <see cref="ConnectionState.Connected"/> and so should be ready for 
    ///         comunication immediately.
    ///     </para>
    ///     <para>
    ///         Each time a client connects the <see cref="NewConnection"/> event will be invoked to alert all subscribers to
    ///         the new connection. A disconnected event is then present on the <see cref="Connection"/> that is passed to the
    ///         subscribers.
    ///     </para>
    /// </remarks>
    /// <threadsafety static="true" instance="true"/>
    public abstract class ConnectionListener : IDisposable
    {
        /// <summary>
        ///     Invoked when a new client connects.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         NewConnection is invoked each time a client connects to the listener. The 
        ///         <see cref="NewConnectionEventArgs"/> contains the new <see cref="Connection"/> for communication with this
        ///         client.
        ///     </para>
        ///     <para>
        ///         Hazel doesn't store connections so it is your responsibility to keep track of the connections to your 
        ///         server. Note that as <see cref="Connection"/> implements <see cref="IDisposable"/> if you are not storing
        ///         a connection then as a bare minimum you should call <see cref="Connection.Dispose()"/> here in order to 
        ///         release the connection correctly.
        ///     </para>
        ///     <include file="DocInclude/common.xml" path="docs/item[@name='Event_Thread_Safety_Warning']/*" />
        /// </remarks>
        /// <example>
        ///     <code language="C#" source="DocInclude/TcpListenerExample.cs"/>
        /// </example>
        public event Action<NewConnectionEventArgs> NewConnection;

        /// <summary>
        ///     Makes this connection listener begin listening for connections.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This instructs the listener to begin listening for new clients connecting to the server. When a new client 
        ///         connects the <see cref="NewConnection"/> event will be invoked containing the connection to the new client.
        ///     </para>
        ///     <para>
        ///         To stop listening you should call <see cref="Dispose()"/>.
        ///     </para>
        /// </remarks>
        /// <example>
        ///     <code language="C#" source="DocInclude/TcpListenerExample.cs"/>
        /// </example>
        public abstract void Start();

        /// <summary>
        ///     Invokes the NewConnection event with the supplied connection.
        /// </summary>
        /// <param name="msg">The user sent bytes that were received as part of the handshake.</param>
        /// <param name="connection">The connection to pass in the arguments.</param>
        /// <remarks>
        ///     Implementers should call this to invoke the <see cref="NewConnection"/> event before data is received so that
        ///     subscribers do not miss any data that may have been sent immediately after connecting.
        /// </remarks>
        protected void InvokeNewConnection(MessageReader msg, Connection connection)
        {
            // Make a copy to avoid race condition between null check and invocation
            Action<NewConnectionEventArgs> handler = NewConnection;
            if (handler != null)
            {
                handler(new NewConnectionEventArgs(msg, connection));
            }
            else
            {
                msg.Recycle();
            }
        }

        /// <summary>
        ///     Closes the connection listener safely.
        /// </summary>
        /// <remarks>
        ///     Internally this simply calls Dispose therefore trying to reuse the ConnectionListener after calling Close will
        ///     cause ObjectDisposedExceptions.
        /// </remarks>
        public virtual void Close()
        {
            Dispose();
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
            this.NewConnection = null;
        }
    }
}
