using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel.UnitTests
{
    public class TestLogger : ILogger
    {
        public void WriteError(string msg)
        {
            Console.WriteLine($"[ERROR] {msg}");
        }

        public void WriteInfo(string msg)
        {
            Console.WriteLine($"[INFO] {msg}");
        }
    }
}
