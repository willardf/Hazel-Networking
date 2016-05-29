using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel
{
    /// <summary>
    ///     Extra internal states for SendOption enumeration when using UDP.
    /// </summary>
    enum SendOptionInternal : byte
    {
        /// <summary>
        ///     Hello message for initiating communication.
        /// </summary>
        Hello = 128,

        /// <summary>
        ///     Message for discontinuing communication.
        /// </summary>
        Disconnect = 129,

        /// <summary>
        ///     Message acknowledging the receipt of a message.
        /// </summary>
        Acknowledgement = 130
    }
}
