using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel
{
    [Flags]
    public enum SendErrors
    {
        None,
        Disconnected,
        Unknown
    }
}
