using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Wrapper for exceptions thrown from Hazel.
    /// </summary>
    class HazelException : Exception
    {
        internal HazelException(string msg) : base (msg)
        {

        }

        internal HazelException(string msg, System.Net.Sockets.SocketException e) : base (msg, e)
        {

        }
    }
}
