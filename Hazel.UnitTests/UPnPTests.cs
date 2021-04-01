using System;
using Hazel.UPnP;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class UPnPTests
    {
        [TestMethod]
        public void CanForwardPort()
        {
            using (UPnPHelper dut = new UPnPHelper(Logger.Instance))
            {
                Assert.IsTrue(dut.ForwardPort(22023, "Hazel Test"));
            }
        }

        [TestMethod]
        public void CanDeletePort()
        {
            using (UPnPHelper dut = new UPnPHelper(Logger.Instance))
            {
                Assert.IsTrue(dut.DeleteForwardingRule(22023));
            }
        }
    }

    public class Logger : ILogger
    {
        public static readonly ILogger Instance = new Logger();

        public void WriteVerbose(string msg)
        {
            Console.WriteLine(msg);
        }

        public void WriteError(string msg)
        {
            Console.WriteLine(msg);
        }

        public void WriteInfo(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
