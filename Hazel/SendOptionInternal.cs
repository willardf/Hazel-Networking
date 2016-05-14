using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel
{
    /// <summary>
    ///     Extra internal states for SendOption enumeration.
    /// </summary>
    enum SendOptionInternal : byte
    {
        /// <summary>
        ///     Hello message for initiating communication.
        /// </summary>
        Hello = 253,

        /// <summary>
        ///     Message for discontinuing communication.
        /// </summary>
        Disconnect = 254,

        /// <summary>
        ///     Message acknowledging the receipt of a message.
        /// </summary>
        Acknowledgement = 255
    }
}
