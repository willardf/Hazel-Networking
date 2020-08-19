using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel
{
    public interface ILogger
    {
        void WriteError(string msg);
        void WriteInfo(string msg);
    }

    public class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        public void WriteError(string msg)
        {
        }

        public void WriteInfo(string msg)
        {
        }
    }

    public class ConsoleLogger : ILogger
    {
        public void WriteError(string msg)
        {
            Console.WriteLine($"{DateTime.Now} [ERROR] {msg}");
        }

        public void WriteInfo(string msg)
        {
            Console.WriteLine($"{DateTime.Now} [INFO] {msg}");
        }
    }
}
