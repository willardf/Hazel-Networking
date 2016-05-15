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
    /// <summary>
    ///     A connection to a remote end point via a network protocol.
    /// </summary>
    public abstract class NetworkConnection : Connection
    {
        /// <summary>
        ///     The remote end point of this connection.
        /// </summary>
        public EndPoint RemoteEndPoint { get; protected set; }
    }
}
