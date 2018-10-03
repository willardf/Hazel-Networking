using Hazel.Udp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Net;
using System.Threading;

namespace Hazel.UnitTests
{
    [TestClass]
    public class BroadcastTests
    {
        [TestMethod]
        public void CanStart()
        {
            const string TestData = "pwerowerower";

            using (UdpBroadcaster caster = new UdpBroadcaster(47777))
            using (UdpBroadcastListener listener = new UdpBroadcastListener(47777))
            {
                listener.StartListen();

                caster.SetData(TestData);

                caster.Broadcast();
                Thread.Sleep(1000);

                var pkt = listener.GetPackets();
                foreach (var p in pkt)
                {
                    Console.WriteLine($"{p.Data} {p.Sender}");
                    Assert.AreEqual(TestData, p.Data);
                }

                Assert.AreEqual(1, pkt.Length);
            }
        }
    }
}
