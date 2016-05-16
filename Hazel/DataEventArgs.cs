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
    public class DataEventArgs : EventArgs, IRecyclable
    {
        /// <summary>
        ///     Object pool for this event.
        /// </summary>
        static readonly ObjectPool<DataEventArgs> objectPool = new ObjectPool<DataEventArgs>(() => new DataEventArgs());

        /// <summary>
        ///     Returns an instance of this object from the pool.
        /// </summary>
        /// <returns></returns>
        internal static DataEventArgs GetObject()
        {
            return objectPool.GetObject();
        }

        /// <summary>
        ///     The bytes received.
        /// </summary>
        public byte[] Bytes { get; private set; }

        /// <summary>
        ///     The SendOption the data was sent with.
        /// </summary>
        public object SendOption { get; private set; }

        /// <summary>
        ///     Private constructor for object pool.
        /// </summary>
        DataEventArgs()
        {

        }

        /// <summary>
        ///     Sets the members of the arguments.
        /// </summary>
        /// <param name="bytes">The bytes received.</param>
        /// <param name="sendOption">The send option used to send the data.</param>
        internal void Set(byte[] bytes, SendOption sendOption)
        {
            this.Bytes = bytes;
            this.SendOption = sendOption;
        }

        /// <summary>
        ///     Returns this object back to the object pool.
        /// </summary>
        public void Recycle()
        {
            objectPool.PutObject(this);
        }
    }
}
