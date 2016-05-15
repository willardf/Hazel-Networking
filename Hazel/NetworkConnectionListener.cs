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
    ///     Connection listener for network based connections.
    /// </summary>
    public abstract class NetworkConnectionListener : ConnectionListener
    {
        /// <summary>
        ///     The IP address we're listening on.
        /// </summary>
        public IPAddress IPAddress { get; protected set; }

        /// <summary>
        ///     The port we're listening on.
        /// </summary>
        public int Port { get; protected set; }
    }
}
