using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazel.UnitTests
{
    [TestClass]
    public class MessageReaderEditingTests
    {
        [DataTestMethod]
        [DataRow(false, false, 0, 1)]
        [DataRow(false, false, 5, 252)]
        [DataRow(false, true, 0, 1)]
        [DataRow(false, true, 5, 252)]
        [DataRow(false, false, 3, 65528)]
        [DataRow(true, false, 0, 1)]
        [DataRow(true, false, 5, 248)]
        [DataRow(true, true, 0, 1)]
        [DataRow(true, true, 5, 248)]
        public void StandaloneEditsUpdateHeader(bool insert, bool clone, int prefixLength, int retainedLength)
        {
            var writer = CreateMessage(prefixLength, retainedLength);
            var source = MessageReader.Get(writer.Buffer, prefixLength);
            var reader = clone ? MessageReader.Get(source) : source;
            var original = (byte[])source.Buffer.Clone();
            try
            {
                EditMessage(reader, insert);
                AssertEditedMessage(reader, insert, retainedLength);
                CollectionAssert.AreEqual(original.Take(prefixLength).ToArray(), reader.Buffer.Take(prefixLength).ToArray());
                if (clone)
                {
                    CollectionAssert.AreEqual(original, source.Buffer);
                    Assert.AreEqual(retainedLength + 7, source.Length);
                    Assert.AreEqual(0, source.Position);
                }
            }
            finally
            {
                if (clone) reader.Recycle();
                source.Recycle();
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ClonedNestedReaderUpdatesOnlyItsOwnHeader(bool insert)
        {
            var writer = new MessageWriter(1024);
            writer.StartMessage(40);
            writer.Write(CreateMessage(0, 1).ToByteArray(true));
            writer.EndMessage();
            var root = MessageReader.Get(writer.Buffer, 0);
            var nested = root.ReadMessage();
            var clone = MessageReader.Get(nested);
            var original = (byte[])writer.Buffer.Clone();
            try
            {
                EditMessage(clone, insert);
                AssertEditedMessage(clone, insert, 1);
                CollectionAssert.AreEqual(original, writer.Buffer);
                Assert.AreEqual(8, nested.Length);
                Assert.AreEqual(11, root.Length);
                CollectionAssert.AreEqual(original.Take(3).ToArray(), clone.Buffer.Take(3).ToArray());
            }
            finally
            {
                clone.Recycle();
                root.Recycle();
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NestedEditsUpdateStandaloneAncestors(bool insert)
        {
            var writer = new MessageWriter(1024);
            writer.StartMessage(40);
            writer.Write(CreateMessage(0, 1).ToByteArray(true));
            writer.EndMessage();
            var root = MessageReader.Get(writer.Buffer, 0);
            try
            {
                var nested = root.ReadMessage();
                EditMessage(nested, insert);
                AssertEditedMessage(nested, insert, 1);
                Assert.AreEqual(nested.Length + 3, root.Length);
                Assert.AreEqual(root.Length, root.Buffer[0] | (root.Buffer[1] << 8));
                var reparsed = MessageReader.Get(root.Buffer, 0);
                try
                {
                    Assert.AreEqual(root.Length, reparsed.Length);
                    Assert.AreEqual(nested.Length, reparsed.ReadMessage().Length);
                    Assert.AreEqual(0, reparsed.BytesRemaining);
                }
                finally
                {
                    reparsed.Recycle();
                }
            }
            finally
            {
                root.Recycle();
            }
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void Tag255StillHasAnEditableHeader(bool insert, bool clone)
        {
            var writer = CreateMessage(5, 1, byte.MaxValue);
            var source = MessageReader.Get(writer.Buffer, 5);
            var reader = clone ? MessageReader.Get(source) : source;
            try
            {
                EditMessage(reader, insert);
                Assert.AreEqual(reader.Length, reader.Buffer[5] | (reader.Buffer[6] << 8));
                Assert.AreEqual(byte.MaxValue, reader.Buffer[7]);
                var parent = MessageReader.CopyMessageIntoParent(reader);
                try
                {
                    var reparsed = parent.ReadMessage();
                    Assert.AreEqual(byte.MaxValue, reparsed.Tag);
                    AssertContents(reparsed, insert, 1);
                    Assert.AreEqual(0, parent.BytesRemaining);
                }
                finally
                {
                    parent.Recycle();
                }
            }
            finally
            {
                if (clone) reader.Recycle();
                source.Recycle();
            }
        }

        [DataTestMethod]
        [DataRow(false, 0, false)]
        [DataRow(true, 0, false)]
        [DataRow(false, 3, false)]
        [DataRow(true, 3, false)]
        [DataRow(false, 1, false)]
        [DataRow(true, 4, true)]
        [DataRow(false, 5, true)]
        [DataRow(true, 5, true)]
        public void HeaderlessEditsPreserveNetworkPrefix(bool insert, int prefixLength, bool clone)
        {
            var writer = CreateMessage(prefixLength, 1);
            var source = MessageReader.Get(writer.Buffer);
            source.Offset = prefixLength;
            source.Length = writer.Length - prefixLength;
            source.Position = 0;
            var reader = clone ? MessageReader.Get(source) : source;
            var prefix = reader.Buffer.Take(prefixLength).ToArray();
            try
            {
                var message = reader.ReadMessage();
                EditMessage(message, insert);
                AssertEditedMessage(message, insert, 1);
                Assert.AreEqual(message.Length + 3, reader.Length);
                Assert.AreEqual(0, reader.BytesRemaining);
                CollectionAssert.AreEqual(prefix, reader.Buffer.Take(prefixLength).ToArray());
            }
            finally
            {
                if (clone) reader.Recycle();
                source.Recycle();
            }
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public void BodyOnlyEditsDoNotWriteAHeader(bool insert, bool duplicate)
        {
            var writer = CreateMessage(0, 1);
            var source = MessageReader.Get(writer.Buffer);
            source.Length = writer.Length;
            var reader = duplicate ? source.ReadMessage().Duplicate() : source.ReadMessageAsNewBuffer();
            // Keep insertion clear of the separate tight-capacity tail-copy defect.
            Array.Resize(ref reader.Buffer, 1024);
            try
            {
                Assert.AreEqual(0, reader.Offset);
                EditMessage(reader, insert);
                reader.Position = 0;
                AssertContents(reader, insert, 1);
            }
            finally
            {
                reader.Recycle();
                source.Recycle();
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void RecycledFramedReaderCanBecomeHeaderless(bool sized)
        {
            var writer = CreateMessage(3, 1);
            var original = (byte[])writer.Buffer.Clone();
            var framed = MessageReader.Get(writer.Buffer, 3);
            framed.Recycle();
            var reader = sized ? MessageReader.GetSized(writer.Buffer.Length) : MessageReader.Get(writer.Buffer);
            try
            {
                Assert.AreSame(framed, reader);
                if (sized) Array.Copy(original, reader.Buffer, original.Length);
                reader.Offset = 3;
                reader.Length = writer.Length - 3;
                reader.Position = 0;
                var prefix = reader.Buffer.Take(3).ToArray();
                var message = reader.ReadMessage();
                EditMessage(message, false);
                AssertEditedMessage(message, false, 1);
                CollectionAssert.AreEqual(prefix, reader.Buffer.Take(3).ToArray());
            }
            finally
            {
                reader.Recycle();
            }
        }

        private static MessageWriter CreateMessage(int prefixLength, int retainedLength, byte tag = 42)
        {
            var writer = new MessageWriter(Math.Max(1024, prefixLength + retainedLength + 64));
            writer.Write(Enumerable.Repeat((byte)0xa5, prefixLength).ToArray());
            writer.StartMessage(tag);
            writer.StartMessage(10);
            writer.Write((byte)0x11);
            writer.EndMessage();
            writer.StartMessage(20);
            writer.Write(Enumerable.Repeat((byte)0x22, retainedLength).ToArray());
            writer.EndMessage();
            writer.EndMessage();
            return writer;
        }

        private static void EditMessage(MessageReader reader, bool insert)
        {
            var first = reader.ReadMessage();
            Assert.AreEqual((byte)0x11, first.ReadByte());
            if (insert)
            {
                var writer = new MessageWriter(16);
                writer.Clear(SendOption.Reliable);
                writer.StartMessage(30);
                writer.Write((byte)0x33);
                writer.EndMessage();
                reader.InsertMessage(first, writer);
            }
            else
            {
                reader.RemoveMessage(first);
            }
            Assert.AreEqual(insert ? 8 : 0, reader.Position);
        }

        private static void AssertEditedMessage(MessageReader reader, bool insert, int retainedLength)
        {
            Assert.AreEqual(retainedLength + (insert ? 11 : 3), reader.Length);
            var headerOffset = reader.Offset - 3;
            Assert.AreEqual(reader.Length, reader.Buffer[headerOffset] | (reader.Buffer[headerOffset + 1] << 8));
            var forwarded = new MessageWriter(reader.Length + 10);
            forwarded.Clear(SendOption.Reliable);
            forwarded.CopyFrom(reader);
            forwarded.StartMessage(90);
            forwarded.Write((byte)0xee);
            forwarded.EndMessage();
            var packet = MessageReader.Get(forwarded.ToByteArray(false));
            try
            {
                var message = packet.ReadMessage();
                Assert.AreEqual((byte)42, message.Tag);
                AssertContents(message, insert, retainedLength);
                var sibling = packet.ReadMessage();
                Assert.AreEqual((byte)90, sibling.Tag);
                Assert.AreEqual((byte)0xee, sibling.ReadByte());
                Assert.AreEqual(0, packet.BytesRemaining);
            }
            finally
            {
                packet.Recycle();
            }
        }

        private static void AssertContents(MessageReader reader, bool insert, int retainedLength)
        {
            if (insert)
            {
                var inserted = reader.ReadMessage();
                Assert.AreEqual((byte)30, inserted.Tag);
                Assert.AreEqual((byte)0x33, inserted.ReadByte());
                var first = reader.ReadMessage();
                Assert.AreEqual((byte)10, first.Tag);
                Assert.AreEqual((byte)0x11, first.ReadByte());
            }
            var retained = reader.ReadMessage();
            Assert.AreEqual((byte)20, retained.Tag);
            CollectionAssert.AreEqual(Enumerable.Repeat((byte)0x22, retainedLength).ToArray(), retained.ReadBytes(retainedLength));
            Assert.AreEqual(0, retained.BytesRemaining);
            Assert.AreEqual(0, reader.BytesRemaining);
        }
    }
}
