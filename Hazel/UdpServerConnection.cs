using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

/* 
* Copyright (C) Jamie Read - All Rights Reserved
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential
* Written by Jamie Read <jamie.read@outlook.com>, January 2016
*/

namespace Hazel
{
    class UdpServerConnection : UdpConnection
    {
        /// <summary>
        ///     The connection listener that we use the socket of.
        /// </summary>
        public UdpConnectionListener Listener { get; private set; }

        /// <summary>
        ///     Lock object for the writing to the state of the connection.
        /// </summary>
        Object stateLock = new Object();

        /// <summary>
        ///     Creates a UdpConnection for the virtual connection to the endpoint.
        /// </summary>
        /// <param name="socket"></param>
        internal UdpServerConnection(UdpConnectionListener listener, EndPoint endPoint)
        {
            this.Listener = listener;
            this.RemoteEndPoint = endPoint;
            this.EndPoint = new NetworkEndPoint(endPoint);

            State = ConnectionState.Connected;
        }

        /// <summary>
        ///     Writes an array of bytes to the connection.
        /// </summary>
        /// <param name="bytes">The bytes of the message to send.</param>
        /// <param name="sendOption">The option this data is requested to send with.</param>
        public override void WriteBytes(byte[] bytes, SendOption sendOption = SendOption.None)
        {
            HandleSend(bytes, sendOption);
        }

        /// <summary>
        ///     Writes bytes to the listener to send.
        /// </summary>
        /// <param name="bytes">bytes to send.</param>
        protected override void WriteBytesToConnection(byte[] bytes)
        {
            lock (stateLock)
            {
                if (State != ConnectionState.Connected)
                    throw new InvalidOperationException("Could not send data as this Connection is not connected. Did you disconnect?");

                Listener.SendData(bytes, RemoteEndPoint);
            }
        }

        /// <summary>
        ///     Connects this Connection to a given remote server.
        /// </summary>
        /// <remarks>
        ///     This will always throw an InvalidOperationException.
        /// </remarks>
        public override void Connect(ConnectionEndPoint remoteEndPoint)
        {
            throw new InvalidOperationException("Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
        }

        /// <summary>
        ///     Called by the listener when we have data.
        /// </summary>
        /// <param name="buffer"></param>
        internal void InvokeDataReceived(byte[] buffer)
        {
           byte[] data = HandleReceive(buffer, buffer.Length);

           if (data != null)
                InvokeDataReceived(new DataEventArgs(data, (SendOption)data[0]));
        }

        /// <summary>
        ///     Safely closes this connection.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            //Here we just need to inform the listener we no longer need data.
            if (disposing)
            {
                lock (stateLock)
                {
                    Listener.RemoveConnectionTo(RemoteEndPoint);

                    State = ConnectionState.NotConnected;
                }
            }

            base.Dispose(disposing);
        }
    }
}
