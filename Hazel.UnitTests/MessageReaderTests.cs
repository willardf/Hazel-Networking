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
        public void TestMessage()
        {
            string test = "5 32 0 0 0 22 0 4 4 2 3 208 52 4 0 1 0 0 0 2 209 52 0 0 1 210 52 0 0 1 40 0 2 208 52 4 36 0 0 128 63 0 0 128 63 0 0 192 63 0 0 112 65 1 0 0 0 1 0 0 0 2 0 0 0 1 0 0 0 1 0 0 0 22 0 4 4 2 3 211 52 4 0 1 0 0 0 2 212 52 0 0 1 213 52 0 0 1 40 0 2 211 52 4 36 0 0 128 63 0 0 128 63 0 0 192 63 0 0 112 65 1 0 0 0 1 0 0 0 2 0 0 0 1 0 0 0 1 0 0 0";
            byte[] testValues = test.Replace("-", "").Split(' ').Select(b => byte.Parse(b)).ToArray();


            MessageWriter dataWriter = new MessageWriter(1024);
            dataWriter.Write((byte)5);
            dataWriter.Write(32);
            dataWriter.StartMessage(4); // Start spawn
            dataWriter.WritePacked(4); // Spawn Id = Player
            dataWriter.WritePacked(2); // Owner Id
            dataWriter.WritePacked(3); // Number children

            dataWriter.Write((byte)208); // NetId (packed)
            dataWriter.Write((byte)52);  // NetId (packed)

            dataWriter.StartMessage(1); // Start data
            dataWriter.Write(""); // Name
            dataWriter.Write((byte)0); // Color
            dataWriter.Write((byte)0); // Important Flags
            dataWriter.Write((byte)2); // Player Id
            dataWriter.EndMessage();

            dataWriter.Write((byte)209); // NetId (packed)
            dataWriter.Write((byte)52);  // NetId (packed)

            dataWriter.StartMessage(1); // Start data (None)
            dataWriter.EndMessage();

            dataWriter.Write((byte)210); // NetId (packed)
            dataWriter.Write((byte)52);  // NetId (packed)

            dataWriter.StartMessage(1); // Start data (None)
            dataWriter.EndMessage();

            dataWriter.EndMessage();

            Console.WriteLine($"{string.Join(" ", dataWriter.Buffer.Take(dataWriter.Length))}");
            Console.WriteLine($"{string.Join(" ", testValues.Take(dataWriter.Length))}");

            Assert.AreEqual(22 + 4 + 1 + 3, dataWriter.Length);


            
            MessageReader msg = MessageReader.Get(testValues, 0, testValues.Length);

            Assert.AreEqual(5, msg.Tag);
            Assert.AreEqual(32, msg.ReadInt32());

            while (msg.Position < msg.Length)
            {
                var sub = msg.ReadMessage();
                
                if (sub.Tag == 4) // Spawn
                {
                    uint spawnId = sub.ReadPackedUInt32();
                    int ownerId = sub.ReadPackedInt32();
                    int numChild = sub.ReadPackedInt32();
                    Console.WriteLine($"Spawning {spawnId} for {ownerId} with {numChild} children");
                    for (int i = 0; i < numChild; ++i)
                    {
                        uint childId = sub.ReadPackedUInt32();
                        var childReader = sub.ReadMessage();
                        if (childId == 6736)
                        {
                            string name = childReader.ReadString();
                            byte color = childReader.ReadByte();
                            byte flags = childReader.ReadByte();
                            uint playerId = childReader.ReadByte();
                            Console.WriteLine($"Child {childId} has name='{name}' {color} {flags} {playerId}");
                        }
                        else
                        {
                            Console.WriteLine($"Child {childId} has data ({childReader.Tag}) len={childReader.Length}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Tag: {sub.Tag}\tLength: {sub.Length}\tData = {string.Join(" ", sub.ReadBytes(sub.Length).Select(s => s.ToString()).ToArray())}");
                }

                Console.WriteLine($"Position: {msg.Position}/{msg.Length}");
            }
        }

        [TestMethod]
        public void GetLittleEndian()
        {
            Assert.IsTrue(MessageWriter.IsLittleEndian());
        }
    }
}