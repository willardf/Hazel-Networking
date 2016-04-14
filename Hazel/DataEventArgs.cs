using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/* 
* Copyright (C) Jamie Read - All Rights Reserved
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential
* Written by Jamie Read <jamie.read@outlook.com>, January 2016
*/

namespace Hazel
{
    public class DataEventArgs : EventArgs
    {
        /// <summary>
        ///     The bytes received.
        /// </summary>
        public byte[] Bytes { get; private set; }

        /// <summary>
        ///     The SendOption the data was sent with.
        /// </summary>
        public object SendOption { get; private set; }

        /// <summary>
        ///     Creates DataEventArgs from bytes received.
        /// </summary>
        /// <param name="bytes"></param>
        public DataEventArgs(byte[] bytes, SendOption sendOption)
        {
            this.Bytes = bytes;
            this.SendOption = sendOption;
        }
    }
}
