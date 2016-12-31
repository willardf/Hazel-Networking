using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Specifies how a message should be sent between connections.
    /// </summary>
    [Flags]
    public enum SendOption : byte
    {
        /// <summary>
        ///     Requests unreliable delivery with no framentation.
        /// </summary>
        /// <remarks>
        ///     Sending data using unreliable delivery means that data is not guaranteed to arrive at it's destination nor is 
        ///     it guarenteed to arrive only once. However, unreliable delivery can be faster than other methods and it 
        ///     typically requires a smaller number of protocol bytes than other methods. There is also typically less 
        ///     processing involved and less memory needed as packets are not stored once sent.
        /// </remarks>
        None = 0,

        /// <summary>
        ///     Requests data be sent reliably but with no fragmentation.
        /// </summary>
        /// <remarks>
        ///     Sending data reliably means that data is guarenteed to arrive and to arrive only once. Reliable delivery
        ///     typically requires more processing, more memory (as packets need to be stored in case they need resending), 
        ///     a larger number of protocol bytes and can be slower than unreliable delivery.
        /// </remarks>
        Reliable = 1,
        
        /// <summary>
        ///     Requests data be sent so that large messages are fragmented into smaller chunks of
        ///     data and reassembled when received.
        /// </summary>
        /// <remarks>
        ///     Fragmented messages allow large amounts of data to be transmitted in smaller chunks when using connections 
        ///     that do not support the transmission of large messages. By specifying reliable delivery messages are 
        ///     guaranteed to arrive and to arrive only once but the sending process may require more memory, processing,
        ///     a larger number protocol bytes and may be slower than sending unreliably.
        /// </remarks>
        FragmentedReliable = 2
    }
}
