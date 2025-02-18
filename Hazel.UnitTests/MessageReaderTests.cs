using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class MessageReaderTests
    {
        [TestMethod]
        public void ReadProperInt()
        {
            const int Test1 = int.MaxValue;
            const int Test2 = int.MinValue;

            var msg = new MessageWriter(128);
            msg.StartMessage(1);
            msg.Write(Test1);
            msg.Write(Test2);
            msg.EndMessage();

            Assert.AreEqual(11, msg.Length);
            Assert.AreEqual(msg.Length, msg.Position);

            MessageReader reader = MessageReader.Get(msg.Buffer, 0);
            Assert.AreEqual(Test1, reader.ReadInt32());
            Assert.AreEqual(Test2, reader.ReadInt32());
        }

        [TestMethod]
        public void ReadProperBool()
        {
            const bool Test1 = true;
            const bool Test2 = false;

            var msg = new MessageWriter(128);
            msg.StartMessage(1);
            msg.Write(Test1);
            msg.Write(Test2);
            msg.EndMessage();

            Assert.AreEqual(5, msg.Length);
            Assert.AreEqual(msg.Length, msg.Position);

            MessageReader reader = MessageReader.Get(msg.Buffer, 0);

            Assert.AreEqual(Test1, reader.ReadBoolean());
            Assert.AreEqual(Test2, reader.ReadBoolean());

        }

        [TestMethod]
        public void ReadProperString()
        {
            const string Test1 = "Hello";
            string Test2 = new string(' ', 1024);
            var msg = new MessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(Test1);
            msg.Write(Test2);
            msg.Write(string.Empty);
            msg.EndMessage();

            Assert.AreEqual(msg.Length, msg.Position);

            MessageReader reader = MessageReader.Get(msg.Buffer, 0);

            Assert.AreEqual(Test1, reader.ReadString());
            Assert.AreEqual(Test2, reader.ReadString());
            Assert.AreEqual(string.Empty, reader.ReadString());

        }

        [TestMethod]
        public void ReadProperFloat()
        {
            const float Test1 = 12.34f;

            var msg = new MessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(Test1);
            msg.EndMessage();

            Assert.AreEqual(7, msg.Length);
            Assert.AreEqual(msg.Length, msg.Position);

            MessageReader reader = MessageReader.Get(msg.Buffer, 0);

            Assert.AreEqual(Test1, reader.ReadSingle());
        }

        [TestMethod]
        public void RemoveMessageWorksForCustomMessages()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;

            var msg = new MessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();
            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            MessageReader reader = MessageReader.Get(msg.Buffer);
            reader.Length = msg.Length;

            var zero = reader.ReadMessage();

            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();
            two.RemoveMessage(three);

            // Reader becomes invalid
            Assert.AreNotEqual(Test3, three.ReadByte()); 

            // Unrealistic, but nice. Earlier data is not affected
            Assert.AreEqual(Test0, zero.ReadByte()); 

            // Continuing to read depth-first works
            var four = two.ReadMessage();
            Assert.AreEqual(Test4, four.ReadByte());

            var five = reader.ReadMessage();
            Assert.AreEqual(Test5, five.ReadByte());
        }

        [TestMethod]
        public void RemoveMessageWorksForReliableMessages()
        {
            const int Test3 = 33;
            const uint Test4 = 44;
            const byte Test5 = 55;
            const uint Test6 = 66;
            const uint Test7 = 77;

            // Test using a reliable message
            // This adds a sendoption header that does not fix the same header format
            // created by StartMessage()
            MessageWriter msg = MessageWriter.Get(SendOption.Reliable);

            // Sub message 1
            msg.StartMessage(5);
            msg.Write(Test3);

            // Sub sub message 2
            msg.StartMessage(2);
            msg.WritePacked(Test4);
            msg.Write(Test5);
            msg.WritePacked(Test6);

            msg.EndMessage(); // End sub sub message 2
            msg.EndMessage(); // End sub message 1

            // Sub message 3
            msg.StartMessage(9);
            msg.Write(Test7);
            msg.EndMessage(); // End sub message 3

            // Convert to a message reader in the same way DataReceivedEventArgs
            // is constructed (see: UdpConnection.InvokeDataReceived())
            MessageReader parentReader = MessageReader.Get(msg.Buffer);
            parentReader.Offset = 3;    // B/c reliable
            parentReader.Length = 20;
            parentReader.Position = 0;

            // The total length is 23, but the above offsetting sets the parent reader to consider its
            // length to be its contents, header not included
            Assert.AreEqual(20, parentReader.BytesRemaining);

            MessageReader subMessage1 = parentReader.ReadMessage();

            Assert.AreEqual(7, parentReader.BytesRemaining);

            _ = subMessage1.ReadInt32();
            MessageReader subSubMessage2 = subMessage1.ReadMessage();
            _ = subSubMessage2.ReadPackedUInt32();
            _ = subSubMessage2.ReadByte();
            _ = subSubMessage2.ReadPackedUInt32();
            Assert.AreEqual(7, parentReader.BytesRemaining);
            Assert.AreEqual(0, subMessage1.BytesRemaining);
            Assert.AreEqual(0, subSubMessage2.BytesRemaining);
            subMessage1.RemoveMessage(subSubMessage2);

            // After removal, the parent should look the same
            Assert.AreEqual(7, parentReader.BytesRemaining);
            Assert.AreEqual(0, subMessage1.BytesRemaining);

            // After removal, we can still continue reading messages
            MessageReader subMessage3 = parentReader.ReadMessage();
            uint subMessage3Value = subMessage3.ReadUInt32();

            Assert.AreEqual(Test7, subMessage3Value);
        }

        [TestMethod]
        public void RemoveMessageWorksForUnReliableMessages()
        {
            const int Test3 = 33;
            const uint Test4 = 44;
            const byte Test5 = 55;
            const uint Test6 = 66;
            const uint Test7 = 77;

            // Test using an unreliable message
            // This adds a sendoption header that does not fix the same header format
            // created by StartMessage()
            MessageWriter msg = MessageWriter.Get(SendOption.None);

            // Sub message 1
            msg.StartMessage(5);
            msg.Write(Test3);

            // Sub sub message 2
            msg.StartMessage(2);
            msg.WritePacked(Test4);
            msg.Write(Test5);
            msg.WritePacked(Test6);

            msg.EndMessage(); // End sub sub message 2
            msg.EndMessage(); // End sub message 1

            // Sub message 3
            msg.StartMessage(9);
            msg.Write(Test7);
            msg.EndMessage(); // End sub message 3

            MessageReader parentReader = MessageReader.Get(msg.Buffer);
            parentReader.Offset = 1;    // B/c unreliable
            parentReader.Length = 20;
            parentReader.Position = 0;

            // The total length is 21, but the above offsetting sets the parent reader to consider its
            // length to be its contents, header not included
            Assert.AreEqual(20, parentReader.BytesRemaining);

            MessageReader subMessage1 = parentReader.ReadMessage();

            Assert.AreEqual(7, parentReader.BytesRemaining);

            _ = subMessage1.ReadInt32();
            MessageReader subSubMessage2 = subMessage1.ReadMessage();
            _ = subSubMessage2.ReadPackedUInt32();
            _ = subSubMessage2.ReadByte();
            _ = subSubMessage2.ReadPackedUInt32();
            Assert.AreEqual(7, parentReader.BytesRemaining);
            Assert.AreEqual(0, subMessage1.BytesRemaining);
            Assert.AreEqual(0, subSubMessage2.BytesRemaining);
            subMessage1.RemoveMessage(subSubMessage2);

            // After removal, the parent should look the same
            Assert.AreEqual(7, parentReader.BytesRemaining);
            Assert.AreEqual(0, subMessage1.BytesRemaining);

            // After removal, we can still continue reading messages
            MessageReader subMessage3 = parentReader.ReadMessage();
            uint subMessage3Value = subMessage3.ReadUInt32();

            Assert.AreEqual(Test7, subMessage3Value);
        }

        [TestMethod]
        public void InsertMessageWorksCustomMessages()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;
            const byte TestInsert = 66;

            var msg = new MessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();
            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            MessageReader reader = MessageReader.Get(msg.Buffer);

            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(5);
            writer.Write(TestInsert);
            writer.EndMessage();

            reader.ReadMessage();
            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();

            two.InsertMessage(three, writer);

            //set the position back to zero to read back the updated message
            reader.Position = 0;

            var zero = reader.ReadMessage();
            Assert.AreEqual(Test0, zero.ReadByte());
            one = reader.ReadMessage();
            two = one.ReadMessage();
            var insert = two.ReadMessage();
            Assert.AreEqual(TestInsert, insert.ReadByte());
            three = two.ReadMessage();
            Assert.AreEqual(Test3, three.ReadByte());
            var four = two.ReadMessage();
            Assert.AreEqual(Test4, four.ReadByte());

            var five = reader.ReadMessage();
            Assert.AreEqual(Test5, five.ReadByte());
        }

        [TestMethod]
        public void InsertMessageWorksForReliableMessages()
        {
            const int Test3 = 33;
            const uint Test4 = 44;
            const byte Test5 = 55;
            const uint Test6 = 66;
            const uint Test7 = 77;
            const uint Test8 = 88;

            // Test using a reliable message
            // This adds a sendoption header that does not fix the same header format
            // created by StartMessage()
            MessageWriter msg = MessageWriter.Get(SendOption.Reliable);

            // Sub message 1
            msg.StartMessage(5);
            msg.Write(Test3);

            // Sub sub message 2
            msg.StartMessage(2);
            msg.WritePacked(Test4);
            msg.Write(Test5);
            msg.WritePacked(Test6);

            msg.EndMessage(); // End sub sub message 2
            msg.EndMessage(); // End sub message 1

            // Sub message 3
            msg.StartMessage(9);
            msg.Write(Test7);
            msg.EndMessage(); // End sub message 3

            // Convert to a message reader in the same way DataReceivedEventArgs
            // is constructed (see: UdpConnection.InvokeDataReceived())
            MessageReader parentReader = MessageReader.Get(msg.Buffer);
            parentReader.Offset = 3;    // B/c reliable
            parentReader.Length = 20;
            parentReader.Position = 0;

            // The total length is 23, but the above offsetting sets the parent reader to consider its
            // length to be its contents, header not included
            Assert.AreEqual(20, parentReader.BytesRemaining);

            MessageReader subMessage1 = parentReader.ReadMessage();

            Assert.AreEqual(7, parentReader.BytesRemaining);

            _ = subMessage1.ReadInt32();
            var subSubMessage2 = subMessage1.ReadMessage();
            _ = subSubMessage2.ReadPackedUInt32();
            _ = subSubMessage2.ReadByte();
            _ = subSubMessage2.ReadPackedUInt32();
            Assert.AreEqual(7, parentReader.BytesRemaining);
            Assert.AreEqual(0, subMessage1.BytesRemaining);
            Assert.AreEqual(0, subSubMessage2.BytesRemaining);

            // Insert an internal message, which shouldn't affect the parent's read
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(11);
            writer.Write(Test8);
            writer.EndMessage();

            // Inserts writer before subSubMessage2
            subMessage1.InsertMessage(subSubMessage2, writer);

            // After insert, the parent should look the same
            Assert.AreEqual(7, parentReader.BytesRemaining);
            Assert.AreEqual(0, subMessage1.BytesRemaining);

            // After insert, we can still continue reading messages
            MessageReader subMessage3 = parentReader.ReadMessage();
            uint subMessage3Value = subMessage3.ReadUInt32();

            Assert.AreEqual(Test7, subMessage3Value);

            // We can now re-read from the start and read the inserted message
            parentReader.Position = 0;
            MessageReader subMessage1Again = parentReader.ReadMessage();

            _ = subMessage1Again.ReadInt32();
            MessageReader insertedMessage = subMessage1Again.ReadMessage();
            uint insertedMessageValue = insertedMessage.ReadUInt32();

            Assert.AreEqual(Test8, insertedMessageValue);

            // After insert, we can still continue reading messages
            MessageReader subMessage3Again = parentReader.ReadMessage();
            uint subMessage3ValueAgain = subMessage3Again.ReadUInt32();

            Assert.AreEqual(Test7, subMessage3ValueAgain);
        }

        [TestMethod]
        public void InsertMessageWorksForUnReliableMessages()
        {
            const int Test3 = 33;
            const uint Test4 = 44;
            const byte Test5 = 55;
            const uint Test6 = 66;
            const uint Test7 = 77;
            const uint Test8 = 88;

            // Test using an unreliable message
            // This adds a sendoption header that does not fix the same header format
            // created by StartMessage()
            MessageWriter msg = MessageWriter.Get(SendOption.None);

            // Sub message 1
            msg.StartMessage(5);
            msg.Write(Test3);

            // Sub sub message 2
            msg.StartMessage(2);
            msg.WritePacked(Test4);
            msg.Write(Test5);
            msg.WritePacked(Test6);

            msg.EndMessage(); // End sub sub message 2
            msg.EndMessage(); // End sub message 1

            // Sub message 3
            msg.StartMessage(9);
            msg.Write(Test7);
            msg.EndMessage(); // End sub message 3

            // Convert to a message reader in the same way DataReceivedEventArgs
            // is constructed (see: UdpConnection.InvokeDataReceived())
            MessageReader parentReader = MessageReader.Get(msg.Buffer);
            parentReader.Offset = 1;    // B/c unreliable
            parentReader.Length = 20;
            parentReader.Position = 0;

            // The total length is 21, but the above offsetting sets the parent reader to consider its
            // length to be its contents, header not included
            Assert.AreEqual(20, parentReader.BytesRemaining);

            MessageReader subMessage1 = parentReader.ReadMessage();

            Assert.AreEqual(7, parentReader.BytesRemaining);

            _ = subMessage1.ReadInt32();
            var subSubMessage2 = subMessage1.ReadMessage();
            _ = subSubMessage2.ReadPackedUInt32();
            _ = subSubMessage2.ReadByte();
            _ = subSubMessage2.ReadPackedUInt32();
            Assert.AreEqual(7, parentReader.BytesRemaining);
            Assert.AreEqual(0, subMessage1.BytesRemaining);
            Assert.AreEqual(0, subSubMessage2.BytesRemaining);

            // Insert an internal message, which shouldn't affect the parent's read
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(11);
            writer.Write(Test8);
            writer.EndMessage();

            // Inserts writer before subSubMessage2
            subMessage1.InsertMessage(subSubMessage2, writer);

            // After insert, the parent should look the same
            Assert.AreEqual(7, parentReader.BytesRemaining);
            Assert.AreEqual(0, subMessage1.BytesRemaining);

            // After insert, we can still continue reading messages
            MessageReader subMessage3 = parentReader.ReadMessage();
            uint subMessage3Value = subMessage3.ReadUInt32();

            Assert.AreEqual(Test7, subMessage3Value);

            // We can now re-read from the start and read the inserted message
            parentReader.Position = 0;
            MessageReader subMessage1Again = parentReader.ReadMessage();

            _ = subMessage1Again.ReadInt32();
            MessageReader insertedMessage = subMessage1Again.ReadMessage();
            uint insertedMessageValue = insertedMessage.ReadUInt32();

            Assert.AreEqual(Test8, insertedMessageValue);

            // After insert, we can still continue reading messages
            MessageReader subMessage3Again = parentReader.ReadMessage();
            uint subMessage3ValueAgain = subMessage3Again.ReadUInt32();

            Assert.AreEqual(Test7, subMessage3ValueAgain);
        }

        [TestMethod]
        public void InsertMessageWorksWithSendOptionNone()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;
            const byte TestInsert = 66;

            var msg = new MessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();
            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            MessageReader reader = MessageReader.Get(msg.Buffer);

            MessageWriter writer = MessageWriter.Get(SendOption.None);
            writer.StartMessage(5);
            writer.Write(TestInsert);
            writer.EndMessage();

            reader.ReadMessage();
            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();

            two.InsertMessage(three, writer);

            //set the position back to zero to read back the updated message
            reader.Position = 0;

            var zero = reader.ReadMessage();
            Assert.AreEqual(Test0, zero.ReadByte());
            one = reader.ReadMessage();
            two = one.ReadMessage();
            var insert = two.ReadMessage();
            Assert.AreEqual(TestInsert, insert.ReadByte());
            three = two.ReadMessage();
            Assert.AreEqual(Test3, three.ReadByte());
            var four = two.ReadMessage();
            Assert.AreEqual(Test4, four.ReadByte());

            var five = reader.ReadMessage();
            Assert.AreEqual(Test5, five.ReadByte());

        }

        [TestMethod]
        public void InsertMessageWithoutStartMessageInWriter()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;
            const byte TestInsert = 66;

            var msg = new MessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();
            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            MessageReader reader = MessageReader.Get(msg.Buffer);

            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.Write(TestInsert);

            reader.ReadMessage();
            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();

            two.InsertMessage(three, writer);

            //set the position back to zero to read back the updated message
            reader.Position = 0;

            var zero = reader.ReadMessage();
            Assert.AreEqual(Test0, zero.ReadByte());
            one = reader.ReadMessage();
            two = one.ReadMessage();
            Assert.AreEqual(TestInsert, two.ReadByte());
            three = two.ReadMessage();
            Assert.AreEqual(Test3, three.ReadByte());
            var four = two.ReadMessage();
            Assert.AreEqual(Test4, four.ReadByte());

            var five = reader.ReadMessage();
            Assert.AreEqual(Test5, five.ReadByte());
        }

        [TestMethod]
        public void InsertMessageWithMultipleMessagesInWriter()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;
            const byte TestInsert = 66;
            const byte TestInsert2 = 77;

            var msg = new MessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();
            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            MessageReader reader = MessageReader.Get(msg.Buffer);

            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(5);
            writer.Write(TestInsert);
            writer.EndMessage();

            writer.StartMessage(6);
            writer.Write(TestInsert2);
            writer.EndMessage();

            reader.ReadMessage();
            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();

            two.InsertMessage(three, writer);

            //set the position back to zero to read back the updated message
            reader.Position = 0;

            var zero = reader.ReadMessage();
            Assert.AreEqual(Test0, zero.ReadByte());
            one = reader.ReadMessage();
            two = one.ReadMessage();
            var insert = two.ReadMessage();
            Assert.AreEqual(TestInsert, insert.ReadByte());
            var insert2 = two.ReadMessage();
            Assert.AreEqual(TestInsert2, insert2.ReadByte());
            three = two.ReadMessage();
            Assert.AreEqual(Test3, three.ReadByte());
            var four = two.ReadMessage();
            Assert.AreEqual(Test4, four.ReadByte());

            var five = reader.ReadMessage();
            Assert.AreEqual(Test5, five.ReadByte());
        }

        [TestMethod]
        public void InsertMessageMultipleInsertsWithoutReset()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;
            const byte Test6 = 66;
            const byte TestInsert = 77;
            const byte TestInsert2 = 88;

            var msg = new MessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            msg.EndMessage();

            msg.StartMessage(67);
            msg.Write(Test6);
            msg.EndMessage();

            MessageReader reader = MessageReader.Get(msg.Buffer);

            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(5);
            writer.Write(TestInsert);
            writer.EndMessage();

            MessageWriter writer2 = MessageWriter.Get(SendOption.Reliable);
            writer2.StartMessage(6);
            writer2.Write(TestInsert2);
            writer2.EndMessage();

            reader.ReadMessage();
            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();

            two.InsertMessage(three, writer);

            // three becomes invalid
            Assert.AreNotEqual(Test3, three.ReadByte());

            // Continuing to read works
            var four = two.ReadMessage();
            Assert.AreEqual(Test4, four.ReadByte());

            var five = one.ReadMessage();
            Assert.AreEqual(Test5, five.ReadByte());

            reader.InsertMessage(one, writer2);

            var six = reader.ReadMessage();
            Assert.AreEqual(Test6, six.ReadByte());
        }

        [TestMethod]
        public void CopySubMessage()
        {
            const byte Test1 = 12;
            const byte Test2 = 146;

            var msg = new MessageWriter(2048);
            msg.StartMessage(1);

            msg.StartMessage(2);
            msg.Write(Test1);
            msg.Write(Test2);
            msg.EndMessage();

            msg.EndMessage();

            MessageReader handleMessage = MessageReader.Get(msg.Buffer, 0);
            Assert.AreEqual(1, handleMessage.Tag);

            var parentReader = MessageReader.Get(handleMessage);

            handleMessage.Recycle();
            SetZero(handleMessage);

            Assert.AreEqual(1, parentReader.Tag);

            for (int i = 0; i < 5; ++i)
            {

                var reader = parentReader.ReadMessage();
                Assert.AreEqual(2, reader.Tag);
                Assert.AreEqual(Test1, reader.ReadByte());
                Assert.AreEqual(Test2, reader.ReadByte());

                var temp = parentReader;
                parentReader = MessageReader.CopyMessageIntoParent(reader);

                temp.Recycle();
                SetZero(temp);
                SetZero(reader);
            }
        }

        [TestMethod]
        public void ReadMessageLength()
        {
            var msg = new MessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(65534);
            msg.StartMessage(2);
            msg.Write("HO");
            msg.EndMessage();
            msg.StartMessage(2);
            msg.EndMessage();
            msg.EndMessage();

            Assert.AreEqual(msg.Length, msg.Position);

            MessageReader reader = MessageReader.Get(msg.Buffer, 0);
            Assert.AreEqual(1, reader.Tag);
            Assert.AreEqual(65534, reader.ReadInt32()); // Content

            var sub = reader.ReadMessage();
            Assert.AreEqual(3, sub.Length);
            Assert.AreEqual(2, sub.Tag);
            Assert.AreEqual("HO", sub.ReadString());

            sub = reader.ReadMessage();
            Assert.AreEqual(0, sub.Length);
            Assert.AreEqual(2, sub.Tag);
        }

        [TestMethod]
        public void ReadMessageAsNewBufferLength()
        {
            var msg = new MessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(65534);
            msg.StartMessage(2);
            msg.Write("HO");
            msg.EndMessage();
            msg.StartMessage(232);
            msg.EndMessage();
            msg.EndMessage();

            Assert.AreEqual(msg.Length, msg.Position);

            MessageReader reader = MessageReader.Get(msg.Buffer, 0);
            Assert.AreEqual(1, reader.Tag);
            Assert.AreEqual(65534, reader.ReadInt32()); // Content

            var sub = reader.ReadMessageAsNewBuffer();
            Assert.AreEqual(0, sub.Position);
            Assert.AreEqual(0, sub.Offset);

            Assert.AreEqual(3, sub.Length);
            Assert.AreEqual(2, sub.Tag);
            Assert.AreEqual("HO", sub.ReadString());

            sub.Recycle();

            sub = reader.ReadMessageAsNewBuffer();
            Assert.AreEqual(0, sub.Position);
            Assert.AreEqual(0, sub.Offset);

            Assert.AreEqual(0, sub.Length);
            Assert.AreEqual(232, sub.Tag);
            sub.Recycle();
        }

        [TestMethod]
        public void ReadStringProtectsAgainstOverrun()
        {
            const string TestDataFromAPreviousPacket = "You shouldn't be able to see this data";

            // An extra byte from the length of TestData when written via MessageWriter
            int DataLength = TestDataFromAPreviousPacket.Length + 1;

            // THE BUG
            //
            // No bound checks. When the server wants to read a string from
            // an offset, it reads the packed int at that offset, treats it
            // as a length and then proceeds to read the data that comes after
            // it without any bound checks. This can be chained with something
            // else to create an infoleak.

            MessageWriter writer = MessageWriter.Get(SendOption.None);

            // This will be our malicious "string length"
            writer.WritePacked(DataLength);

            // This is data from a "previous packet"
            writer.Write(TestDataFromAPreviousPacket);

            byte[] testData = writer.ToByteArray(includeHeader: false);

            // One extra byte for the MessageWriter header, one more for the malicious data
            Assert.AreEqual(DataLength + 1, testData.Length);

            var dut = MessageReader.Get(testData);

            // If Length is short by even a byte, ReadString should obey that.
            dut.Length--;

            try
            {
                dut.ReadString();
                Assert.Fail("ReadString is expected to throw");
            }
            catch (InvalidDataException) { }
        }

        [TestMethod]
        public void ReadMessageProtectsAgainstOverrun()
        {
            const string TestDataFromAPreviousPacket = "You shouldn't be able to see this data";
            
            // An extra byte from the length of TestData when written via MessageWriter
            // Extra 3 bytes for the length + tag header for ReadMessage.
            int DataLength = TestDataFromAPreviousPacket.Length + 1 + 3;

            // THE BUG
            //
            // No bound checks. When the server wants to read a message, it
            // reads the uint16 at that offset, treats it as a length without any bound checks.
            // This can be allow a later ReadString or ReadBytes to create an infoleak.

            MessageWriter writer = MessageWriter.Get(SendOption.None);

            // This is the malicious length. No data in this message, so it should be zero.
            writer.Write((ushort)1); 
            writer.Write((byte)0); // Tag

            // This is data from a "previous packet"
            writer.Write(TestDataFromAPreviousPacket);

            byte[] testData = writer.ToByteArray(includeHeader: false);

            Assert.AreEqual(DataLength, testData.Length);

            var outer = MessageReader.Get(testData);

            // Length is just the malicious message header.
            outer.Length = 3;

            try
            {
                outer.ReadMessage();
                Assert.Fail("ReadMessage is expected to throw");
            }
            catch (InvalidDataException) { }
        }

        [TestMethod]
        public void GetLittleEndian()
        {
            Assert.IsTrue(MessageWriter.IsLittleEndian());
        }

        private void SetZero(MessageReader reader)
        {
            for (int i = 0; i < reader.Buffer.Length; ++i)
                reader.Buffer[i] = 0;
        }
    }

}