using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel
{
    /// <summary>
    ///     Specifies how a message should be sent.
    /// </summary>
    [Flags]
    public enum SendOption : byte
    {
        /// <summary>
        ///     Requests unreliable delivery with no framentation or ordering.
        /// </summary>
        None = 0,

        /// <summary>
        ///     Requests data be sent reliably. Data is guaranteed to arrive at it's destination.
        /// </summary>
        Reliable = 1,

        /// <summary>
        ///     Requests that data should be sent in order.
        /// </summary>
        /// <remarks>
        ///     Any packets that are out of order in this option will be dropped.
        /// </remarks>
        Ordered = 2,

        /// <summary>
        ///     Requests that data should be sent in order and reliably.
        /// </summary>
        /// <remarks>
        ///     Only messages that are sent using OrderedReliable or OrderedFragmentedReliable will arrive
        ///     in order, other messages
        ///     may arrive in between.
        /// </remarks>
        OrderedReliable = 3,

        /// <summary>
        ///     Requests data be sent so that large messages are fragmented into smaller chunks of
        ///     data and reassembled when received.
        /// </summary>
        FragmentedReliable = 5,

        /// <summary>
        ///     Requests data be sent so that large messages are fragmented into smaller chunks of data and
        ///     reassembled when received and that the message arrives in order with other messages.
        /// </summary>
        OrderedFragmentedReliable = 7
    }
}
