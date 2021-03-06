using System;
using System.Collections.Generic;
using Hazel.Udp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class UdpReliabilityTests
    {
        [TestMethod]
        public void TestThatAllMessagesAreReceived()
        {
            List<MessageReader> messagesReceived = new List<MessageReader>();

            UdpConnectionTestHarness dut = new UdpConnectionTestHarness();
            dut.DataReceived += evt =>
            {
                messagesReceived.Add(evt.Message);
            };

            MessageWriter data = MessageWriter.Get(SendOption.Reliable);

            for (int i = 1; i < ushort.MaxValue * 2; ++i)
            {
                // Send a new message, it should be received and ack'd
                SetReliableId(data, i);
                dut.Test_Receive(data);

                // Resend an old message, it should be ignored
                if (i > 2)
                {
                    SetReliableId(data, i - 1);
                    dut.Test_Receive(data);

                    // It should still be ack'd
                    Assert.AreEqual(2, dut.BytesSent.Count);
                    dut.BytesSent.RemoveAt(1);
                }

                Assert.AreEqual(1, messagesReceived.Count);
                messagesReceived.Clear();

                Assert.AreEqual(1, dut.BytesSent.Count);
                dut.BytesSent.Clear();
            }
        }

        [TestMethod]
        public void TestAcksForNotReceivedMessages()
        {
            List<MessageReader> messagesReceived = new List<MessageReader>();

            UdpConnectionTestHarness dut = new UdpConnectionTestHarness();
            dut.DataReceived += evt =>
            {
                messagesReceived.Add(evt.Message);
            };

            MessageWriter data = MessageWriter.Get(SendOption.Reliable);

            SetReliableId(data, 1);
            dut.Test_Receive(data);

            SetReliableId(data, 3);
            dut.Test_Receive(data);

            MessageReader ackPacket = dut.BytesSent[1];
            // Must be ack
            Assert.AreEqual(4, ackPacket.Length);

            byte recentPackets = ackPacket.Buffer[3];
            // Last packet was not received
            Assert.AreEqual(0, recentPackets & 1);
            // The packet before that was.
            Assert.AreEqual(1, (recentPackets >> 1) & 1);
        }

        private static void SetReliableId(MessageWriter data, int i)
        {
            ushort id = (ushort)i;
            data.Buffer[1] = (byte)(id >> 8);
            data.Buffer[2] = (byte)id;
        }
    }
}
