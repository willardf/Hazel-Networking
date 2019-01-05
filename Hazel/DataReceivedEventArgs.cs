using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    public struct DataReceivedEventArgs
    {
        /// <summary>
        ///     The bytes received from the client.
        /// </summary>
        public readonly MessageReader Message;

        /// <summary>
        ///     The <see cref="SendOption"/> the data was sent with.
        /// </summary>
        public readonly SendOption SendOption;
        
        public DataReceivedEventArgs(MessageReader msg, SendOption sendOption)
        {
            this.Message = msg;
            this.SendOption = sendOption;
        }
    }
}
