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
        public void ReadMessageLength()
        {
            var msg = new MessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(65534);
            msg.StartMessage(2);
            msg.Write("HO");
            msg.EndMessage();
            msg.StartMessage(2);
            msg.Write("NO");
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
            Assert.AreEqual(3, sub.Length);
            Assert.AreEqual(2, sub.Tag);
            Assert.AreEqual("NO", sub.ReadString());
        }

        [TestMethod]
        public void GetLittleEndian()
        {
            Assert.IsTrue(MessageWriter.IsLittleEndian());
        }

        // [TestMethod]
        public void Test()
        {
            string dataStr = "4 0 5 32 0 0 0 6 0 1 5";
            byte[] data = dataStr.Split(' ').Select(b => byte.Parse(b)).ToArray();
            MessageReader readerParent1 = MessageReader.Get(data);
            while (readerParent1.Position < readerParent1.Length)
            {
                var readerParent = readerParent1.ReadMessage(); // Loop of InnerNetClient
                while (readerParent.Position < readerParent.Length)
                {

                    Console.WriteLine($"{readerParent.Tag} = {readerParent.Length}");
                    switch (readerParent.Tag)
                    {
                        case 1:
                            {
                                int gameIdConfirm = readerParent.ReadInt32();
                                break;
                            }
                        case 5:
                            {
                                int gameIdConfirm = readerParent.ReadInt32();
                                HandleGameData(readerParent);
                                break;
                            }
                        case 6:
                            {
                                int gameIdConfirm = readerParent.ReadInt32();
                                int targetId = readerParent.ReadPackedInt32(); // Skip target id
                                HandleGameData(readerParent);
                            }
                            break;
                    }
                }
            }
        }

        private static void HandleGameData(MessageReader readerParent)
        {
            while (readerParent.Position < readerParent.Length)
            {
                var reader = readerParent.ReadMessage();

                switch (reader.Tag)
                {
                    case 4:
                        {
                            Console.WriteLine($"\t{reader.Tag} = SpawnId: {reader.ReadPackedUInt32()}");
                            Console.WriteLine($"\t{reader.Tag} = OwnerId: {reader.ReadPackedInt32()}");
                            Console.WriteLine($"\t{reader.Tag} = Flags: {reader.ReadByte()}");
                            var numChildren = reader.ReadPackedInt32();
                            Console.WriteLine($"\t{reader.Tag} = NumChildren: {numChildren}");
                            for (int i = 0; i < numChildren; ++i)
                            {
                                Console.WriteLine($"\t{reader.Tag} = NetId: {reader.ReadPackedUInt32()}");
                                var datam = reader.ReadMessage();
                                Console.WriteLine($"\t{reader.Tag} = Data: {string.Join(" ", datam.ReadBytes(datam.Length))}");
                            }
                            break;
                        }
                    default:
                        {
                            Console.WriteLine($"\t{reader.Tag} = {string.Join(" ", reader.ReadBytes(reader.Length))}");
                        }
                        break;
                }

                Console.WriteLine();
            }
        }
    }
}