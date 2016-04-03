using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;


/* 
* Copyright (C) Jamie Read - All Rights Reserved
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential
* Written by Jamie Read <jamie.read@outlook.com>, January 2016
*/

namespace Hazel
{
    /// <summary>
    ///     Handles the sending and receiving of messages through the channel to give connection orientated, packet based transmission.
    /// </summary>
    public abstract class Connection : IDisposable
    {
        /// <summary>
        ///     Called when a message has been received.
        /// </summary>
        public event EventHandler<DataEventArgs> DataReceived;

        /// <summary>
        ///     Called when the end point disconnects from us or an error occurs.
        /// </summary>
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        /// <summary>
        ///     The end point of this Connection.
        /// </summary>
        public ConnectionEndPoint EndPoint { get; protected set; }

        /// <summary>
        ///     The traffic statistics about this Connection.
        /// </summary>
        public ConnectionStatistics Statistics { get; protected set; }

        /// <summary>
        ///     The state of this connection.
        /// </summary>
        public ConnectionState State { get { return state; } protected set { state = value; } }
        volatile ConnectionState state;

        /// <summary>
        ///     Constructor that initializes the ConnecitonStatistics object.
        /// </summary>
        protected Connection()
        {
            Statistics = new ConnectionStatistics();

            State = ConnectionState.NotConnected;
        }

        /// <summary>
        ///     Writes an array of bytes to the connection and prefixes the length.
        /// </summary>
        /// <param name="bytes">The bytes of the message to send.</param>
        /// <param name="sendOption">The options this data is requested to send with.</param>
        /// <remarks>
        ///     The sendOptions parameter is only a request to use those options and the actual method used to send the
        ///     data is up to the implementation. There are circumstances where this parameter may be ignored but in 
        ///     general any implementer should aim to always follow the user's request here.
        /// </remarks>
        public abstract void WriteBytes(byte[] bytes, SendOption sendOption = SendOption.None);

        /// <summary>
        ///     Connects the connection to a remote server and begins listening.
        /// </summary>
        public abstract void Connect(ConnectionEndPoint remoteEndPoint);

        /// <summary>
        ///     Invokes the DataReceived event to alert subscribers we received data.
        /// </summary>
        /// <param name="args">The arguments to supply.</param>
        protected void InvokeDataReceived(DataEventArgs args)
        {
            //Make a copy to avoid race condition between null check and invocation
            EventHandler<DataEventArgs> handler = DataReceived;
            if (handler != null)
                handler(this, args);
        }

        /// <summary>
        ///     Invokes the Disconnected event to alert hooked up methods there was an error or the remote end point disconnected.
        /// </summary>
        /// <param name="args">The arguments to supply.</param>
        protected void InvokeDisconnected(DisconnectedEventArgs args)
        {
            //Make a copy to avoid race condition between null check and invocation
            EventHandler<DisconnectedEventArgs> handler = Disconnected;
            if (handler != null)
                handler(this, args);
        }

        /// <summary>
        ///     Closes this connections safely.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        ///     Disposes of this NetworkConnection.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Disposes of this NetworkConnection.
        /// </summary>
        /// <param name="disposing">Are we currently disposing?</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }
    }
}
