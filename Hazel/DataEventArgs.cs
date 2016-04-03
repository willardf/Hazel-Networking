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
        public byte[] Bytes;

        /// <summary>
        ///     Creates DataEventArgs from bytes received.
        /// </summary>
        /// <param name="bytes"></param>
        public DataEventArgs(byte[] bytes)
        {
            this.Bytes = bytes;
        }
    }
}
