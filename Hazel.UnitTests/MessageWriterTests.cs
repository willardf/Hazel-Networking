using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class MessageWriterTests
    {
        [TestMethod]
        public void WriteProperInt()
        {
            const int Test1 = int.MaxValue;
            const int Test2 = int.MinValue;

            var msg = new MessageWriter(128);
            msg.Write(Test1);
            msg.Write(Test2);

            Assert.AreEqual(8, msg.Length);
            Assert.AreEqual(msg.Length, msg.Position);

            using (MemoryStream m = new MemoryStream(msg.Buffer, 0, msg.Length))
            using (BinaryReader reader = new BinaryReader(m))
            {
                Assert.AreEqual(Test1, reader.ReadInt32());
                Assert.AreEqual(Test2, reader.ReadInt32());
            }
        }

        [TestMethod]
        public void WriteProperBool()
        {
            const bool Test1 = true;
            const bool Test2 = false;

            var msg = new MessageWriter(128);
            msg.Write(Test1);
            msg.Write(Test2);

            Assert.AreEqual(2, msg.Length);
            Assert.AreEqual(msg.Length, msg.Position);

            using (MemoryStream m = new MemoryStream(msg.Buffer, 0, msg.Length))
            using (BinaryReader reader = new BinaryReader(m))
            {
                Assert.AreEqual(Test1, reader.ReadBoolean());
                Assert.AreEqual(Test2, reader.ReadBoolean());
            }
        }

        [TestMethod]
        public void WriteProperString()
        {
            const string Test1 = "Hello";
            string Test2 = new string(' ', 1024);
            var msg = new MessageWriter(2048);
            msg.Write(Test1);
            msg.Write(Test2);

            Assert.AreEqual(msg.Length, msg.Position);

            using (MemoryStream m = new MemoryStream(msg.Buffer, 0, msg.Length))
            using (BinaryReader reader = new BinaryReader(m))
            {
                Assert.AreEqual(Test1, reader.ReadString());
                Assert.AreEqual(Test2, reader.ReadString());
            }
        }

        [TestMethod]
        public void WriteProperFloat()
        {
            const float Test1 = 12.34f;

            var msg = new MessageWriter(2048);
            msg.Write(Test1);

            Assert.AreEqual(msg.Length, msg.Position);

            using (MemoryStream m = new MemoryStream(msg.Buffer, 0, msg.Length))
            using (BinaryReader reader = new BinaryReader(m))
            {
                Assert.AreEqual(Test1, reader.ReadSingle());
            }
        }

        [TestMethod]
        public void WritesMessageLength()
        {
            var msg = new MessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(65534);
            msg.EndMessage();

            Assert.AreEqual(2 + 1 + 4, msg.Position);
            Assert.AreEqual(msg.Length, msg.Position);

            using (MemoryStream m = new MemoryStream(msg.Buffer, 0, msg.Length))
            using (BinaryReader reader = new BinaryReader(m))
            {
                Assert.AreEqual(4, reader.ReadUInt16()); // Length After Type and Target
                Assert.AreEqual(1, reader.ReadByte()); // Type
                Assert.AreEqual(65534, reader.ReadInt32()); // Content
            }
        }

        [TestMethod]
        public void GetLittleEndian()
        {
            Assert.IsTrue(MessageWriter.IsLittleEndian());
        }
    }
}
