using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    /// <summary>
    ///     Interface for all items that can be returned to an object pool.
    /// </summary>
    interface IRecyclable
    {
        /// <summary>
        ///     Returns this object back to the object pool.
        /// </summary>
        void Recycle();
    }
}
