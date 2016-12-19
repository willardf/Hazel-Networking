using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class StatisticsTests
    {
        [TestMethod]
        public void SendTests()
        {
            ConnectionStatistics statistics = new ConnectionStatistics();

            statistics.LogUnreliableSend(10, 11);

            Assert.AreEqual(1, statistics.MessagesSent);
            Assert.AreEqual(1, statistics.UnreliableMessagesSent);
            Assert.AreEqual(0, statistics.ReliableMessagesSent);
            Assert.AreEqual(0, statistics.FragmentedMessagesSent);
            Assert.AreEqual(0, statistics.AcknowledgementMessagesSent);
            Assert.AreEqual(0, statistics.HelloMessagesSent);

            Assert.AreEqual(10, statistics.DataBytesSent);
            Assert.AreEqual(11, statistics.TotalBytesSent);

            statistics.LogReliableSend(5, 8);

            Assert.AreEqual(2, statistics.MessagesSent);
            Assert.AreEqual(1, statistics.UnreliableMessagesSent);
            Assert.AreEqual(1, statistics.ReliableMessagesSent);
            Assert.AreEqual(0, statistics.FragmentedMessagesSent);
            Assert.AreEqual(0, statistics.AcknowledgementMessagesSent);
            Assert.AreEqual(0, statistics.HelloMessagesSent);

            Assert.AreEqual(15, statistics.DataBytesSent);
            Assert.AreEqual(19, statistics.TotalBytesSent);

            statistics.LogFragmentedSend(6, 10);

            Assert.AreEqual(3, statistics.MessagesSent);
            Assert.AreEqual(1, statistics.UnreliableMessagesSent);
            Assert.AreEqual(1, statistics.ReliableMessagesSent);
            Assert.AreEqual(1, statistics.FragmentedMessagesSent);
            Assert.AreEqual(0, statistics.AcknowledgementMessagesSent);
            Assert.AreEqual(0, statistics.HelloMessagesSent);

            Assert.AreEqual(21, statistics.DataBytesSent);
            Assert.AreEqual(29, statistics.TotalBytesSent);

            statistics.LogAcknowledgementSend(4);

            Assert.AreEqual(4, statistics.MessagesSent);
            Assert.AreEqual(1, statistics.UnreliableMessagesSent);
            Assert.AreEqual(1, statistics.ReliableMessagesSent);
            Assert.AreEqual(1, statistics.FragmentedMessagesSent);
            Assert.AreEqual(1, statistics.AcknowledgementMessagesSent);
            Assert.AreEqual(0, statistics.HelloMessagesSent);

            Assert.AreEqual(21, statistics.DataBytesSent);
            Assert.AreEqual(33, statistics.TotalBytesSent);

            statistics.LogHelloSend(7);

            Assert.AreEqual(5, statistics.MessagesSent);
            Assert.AreEqual(1, statistics.UnreliableMessagesSent);
            Assert.AreEqual(1, statistics.ReliableMessagesSent);
            Assert.AreEqual(1, statistics.FragmentedMessagesSent);
            Assert.AreEqual(1, statistics.AcknowledgementMessagesSent);
            Assert.AreEqual(1, statistics.HelloMessagesSent);

            Assert.AreEqual(21, statistics.DataBytesSent);
            Assert.AreEqual(40, statistics.TotalBytesSent);
            
            Assert.AreEqual(0, statistics.MessagesReceived);
            Assert.AreEqual(0, statistics.UnreliableMessagesReceived);
            Assert.AreEqual(0, statistics.ReliableMessagesReceived);
            Assert.AreEqual(0, statistics.FragmentedMessagesReceived);
            Assert.AreEqual(0, statistics.AcknowledgementMessagesReceived);
            Assert.AreEqual(0, statistics.HelloMessagesReceived);

            Assert.AreEqual(0, statistics.DataBytesReceived);
            Assert.AreEqual(0, statistics.TotalBytesReceived);
        }

        [TestMethod]
        public void ReceiveTests()
        {
            ConnectionStatistics statistics = new ConnectionStatistics();

            statistics.LogUnreliableReceive(10, 11);

            Assert.AreEqual(1, statistics.MessagesReceived);
            Assert.AreEqual(1, statistics.UnreliableMessagesReceived);
            Assert.AreEqual(0, statistics.ReliableMessagesReceived);
            Assert.AreEqual(0, statistics.FragmentedMessagesReceived);
            Assert.AreEqual(0, statistics.AcknowledgementMessagesReceived);
            Assert.AreEqual(0, statistics.HelloMessagesReceived);

            Assert.AreEqual(10, statistics.DataBytesReceived);
            Assert.AreEqual(11, statistics.TotalBytesReceived);

            statistics.LogReliableReceive(5, 8);

            Assert.AreEqual(2, statistics.MessagesReceived);
            Assert.AreEqual(1, statistics.UnreliableMessagesReceived);
            Assert.AreEqual(1, statistics.ReliableMessagesReceived);
            Assert.AreEqual(0, statistics.FragmentedMessagesReceived);
            Assert.AreEqual(0, statistics.AcknowledgementMessagesReceived);
            Assert.AreEqual(0, statistics.HelloMessagesReceived);

            Assert.AreEqual(15, statistics.DataBytesReceived);
            Assert.AreEqual(19, statistics.TotalBytesReceived);

            statistics.LogFragmentedReceive(6, 10);

            Assert.AreEqual(3, statistics.MessagesReceived);
            Assert.AreEqual(1, statistics.UnreliableMessagesReceived);
            Assert.AreEqual(1, statistics.ReliableMessagesReceived);
            Assert.AreEqual(1, statistics.FragmentedMessagesReceived);
            Assert.AreEqual(0, statistics.AcknowledgementMessagesReceived);
            Assert.AreEqual(0, statistics.HelloMessagesReceived);

            Assert.AreEqual(21, statistics.DataBytesReceived);
            Assert.AreEqual(29, statistics.TotalBytesReceived);

            statistics.LogAcknowledgementReceive(4);

            Assert.AreEqual(4, statistics.MessagesReceived);
            Assert.AreEqual(1, statistics.UnreliableMessagesReceived);
            Assert.AreEqual(1, statistics.ReliableMessagesReceived);
            Assert.AreEqual(1, statistics.FragmentedMessagesReceived);
            Assert.AreEqual(1, statistics.AcknowledgementMessagesReceived);
            Assert.AreEqual(0, statistics.HelloMessagesReceived);

            Assert.AreEqual(21, statistics.DataBytesReceived);
            Assert.AreEqual(33, statistics.TotalBytesReceived);

            statistics.LogHelloReceive(7);

            Assert.AreEqual(5, statistics.MessagesReceived);
            Assert.AreEqual(1, statistics.UnreliableMessagesReceived);
            Assert.AreEqual(1, statistics.ReliableMessagesReceived);
            Assert.AreEqual(1, statistics.FragmentedMessagesReceived);
            Assert.AreEqual(1, statistics.AcknowledgementMessagesReceived);
            Assert.AreEqual(1, statistics.HelloMessagesReceived);

            Assert.AreEqual(21, statistics.DataBytesReceived);
            Assert.AreEqual(40, statistics.TotalBytesReceived);

            Assert.AreEqual(0, statistics.MessagesSent);
            Assert.AreEqual(0, statistics.UnreliableMessagesSent);
            Assert.AreEqual(0, statistics.ReliableMessagesSent);
            Assert.AreEqual(0, statistics.FragmentedMessagesSent);
            Assert.AreEqual(0, statistics.AcknowledgementMessagesSent);
            Assert.AreEqual(0, statistics.HelloMessagesSent);

            Assert.AreEqual(0, statistics.DataBytesSent);
            Assert.AreEqual(0, statistics.TotalBytesSent);
        }
    }
}
