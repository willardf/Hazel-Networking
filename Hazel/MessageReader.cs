using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Hazel
{
    public class MessageReader : IRecyclable
    {
        public static readonly ObjectPool<MessageReader> ReaderPool = new ObjectPool<MessageReader>(() => new MessageReader());

        public byte[] Buffer;
        public byte Tag;

        public int Length;
        public int Offset;

        public int BytesRemaining => this.Length - this.Position;

        private MessageReader Parent;

        public int Position
        {
            get { return this._position; }
            set
            {
                this._position = value;
                this.readHead = value + Offset;
            }
        }

        private int _position;
        private int readHead;

        public static MessageReader GetSized(int minSize)
        {
            var output = ReaderPool.GetObject();

            if (output.Buffer == null || output.Buffer.Length < minSize)
            {
                output.Buffer = new byte[minSize];
            }
            else
            {
                Array.Clear(output.Buffer, 0, output.Buffer.Length);
            }

            output.Offset = 0;
            output.Position = 0;
            output.Tag = byte.MaxValue;
            return output;
        }

        public static MessageReader Get(byte[] buffer)
        {
            var output = ReaderPool.GetObject();

            output.Buffer = buffer;
            output.Offset = 0;
            output.Position = 0;
            output.Length = buffer.Length;
            output.Tag = byte.MaxValue;

            return output;
        }

        public static MessageReader CopyMessageIntoParent(MessageReader source)
        {
            var output = MessageReader.GetSized(source.Length + 3);
            System.Buffer.BlockCopy(source.Buffer, source.Offset - 3, output.Buffer, 0, source.Length + 3);

            output.Offset = 0;
            output.Position = 0;
            output.Length = source.Length + 3;

            return output;
        }

        public static MessageReader Get(MessageReader source)
        {
            var output = MessageReader.GetSized(source.Buffer.Length);
            System.Buffer.BlockCopy(source.Buffer, 0, output.Buffer, 0, source.Buffer.Length);

            output.Offset = source.Offset;

            output._position = source._position;
            output.readHead = source.readHead;

            output.Length = source.Length;
            output.Tag = source.Tag;

            return output;
        }

        public static MessageReader Get(byte[] buffer, int offset)
        {
            // Ensure there is at least a header
            if (offset + 3 > buffer.Length) return null;

            var output = ReaderPool.GetObject();

            output.Buffer = buffer;
            output.Offset = offset;
            output.Position = 0;

            output.Length = output.ReadUInt16();
            output.Tag = output.ReadByte();

            output.Offset += 3;
            output.Position = 0;

            return output;
        }

        /// <summary>
        /// Produces a MessageReader using the parent's buffer. This MessageReader should **NOT** be recycled.
        /// </summary>
        public MessageReader ReadMessage()
        {
            // Ensure there is at least a header
            if (this.BytesRemaining < 3) throw new InvalidDataException($"ReadMessage header is longer than message length: 3 of {this.BytesRemaining}");

            var output = new MessageReader();

            output.Parent = this;
            output.Buffer = this.Buffer;
            output.Offset = this.readHead;
            output.Position = 0;

            output.Length = output.ReadUInt16();
            output.Tag = output.ReadByte();

            output.Offset += 3;
            output.Position = 0;

            if (this.BytesRemaining < output.Length + 3) throw new InvalidDataException($"Message Length at Position {this.readHead} is longer than message length: {output.Length + 3} of {this.BytesRemaining}");

            this.Position += output.Length + 3;
            return output;
        }

        /// <summary>
        /// Produces a MessageReader with a new buffer. This MessageReader should be recycled.
        /// </summary>
        public MessageReader ReadMessageAsNewBuffer()
        {
            if (this.BytesRemaining < 3) throw new InvalidDataException($"ReadMessage header is longer than message length: 3 of {this.BytesRemaining}");

            var len = this.ReadUInt16();
            var tag = this.ReadByte();

            if (this.BytesRemaining < len) throw new InvalidDataException($"Message Length at Position {this.readHead} is longer than message length: {len} of {this.BytesRemaining}");

            var output = MessageReader.GetSized(len);

            output.Parent = this;
            Array.Copy(this.Buffer, this.readHead, output.Buffer, 0, len);

            output.Length = len;
            output.Tag = tag;

            this.Position += output.Length;
            return output;
        }

        public MessageWriter StartWriter()
        {
            var output = new MessageWriter(this.Buffer);
            output.Position = this.readHead;
            return output;
        }

        public MessageReader Duplicate()
        {
            var output = GetSized(this.Length);
            Array.Copy(this.Buffer, this.Offset, output.Buffer, 0, this.Length);
            output.Length = this.Length;
            output.Offset = 0;
            output.Position = 0;

            return output;
        }

        public void RemoveMessage(MessageReader reader)
        {
            var temp = MessageReader.GetSized(reader.Buffer.Length);
            try
            {
                var headerOffset = reader.Offset - 3;
                var endOfMessage = reader.Offset + reader.Length;
                var len = reader.Buffer.Length - endOfMessage;

                Array.Copy(reader.Buffer, endOfMessage, temp.Buffer, 0, len);
                Array.Copy(temp.Buffer, 0, this.Buffer, headerOffset, len);

                this.AdjustLength(reader.Offset, reader.Length + 3);
            }
            finally
            {
                temp.Recycle();
            }
        }

        public void InsertMessage(MessageReader reader, MessageWriter writer)
        {
            var temp = MessageReader.GetSized(reader.Buffer.Length);
            try 
            {
                var headerOffset = reader.Offset - 3;
                var startOfMessage = reader.Offset;
                var len = reader.Buffer.Length - startOfMessage;
                int writerOffset = 3;
                switch (writer.SendOption)
                {
                    case SendOption.Reliable:
                        writerOffset = 3;
                        break;
                    case SendOption.None:
                        writerOffset = 1;
                        break;
                }
                
                //store the original buffer in temp
                Array.Copy(reader.Buffer, headerOffset, temp.Buffer, 0, len);

                //put the contents of writer in at headerOffset
                Array.Copy(writer.Buffer, writerOffset, this.Buffer, headerOffset, writer.Length-writerOffset);

                //put the original buffer in after that
                Array.Copy(temp.Buffer, 0, this.Buffer, headerOffset + (writer.Length-writerOffset), len - writer.Length);

                this.AdjustLength(-1 * reader.Offset , -1 * (writer.Length - writerOffset));
            }
            finally
            {
                temp.Recycle();
            }
        }

        private void AdjustLength(int offset, int amount)
        {
            if (this.readHead > offset)
            {
                this.Position -= amount;
            }

            if (Parent != null)
            {
                var lengthOffset = this.Offset - 3;
                var curLen = this.Buffer[lengthOffset]
                    | (this.Buffer[lengthOffset + 1] << 8);

                curLen -= amount;
                this.Length -= amount;

                this.Buffer[lengthOffset] = (byte)curLen;
                this.Buffer[lengthOffset + 1] = (byte)(this.Buffer[lengthOffset + 1] >> 8);

                Parent.AdjustLength(offset, amount);
            }
        }

        public void Recycle()
        {
            this.Parent = null;
            ReaderPool.PutObject(this);
        }

        #region Read Methods
        public bool ReadBoolean()
        {
            byte val = this.FastByte();
            return val != 0;
        }

        public sbyte ReadSByte()
        {
            return (sbyte)this.FastByte();
        }

        public byte ReadByte()
        {
            return this.FastByte();
        }

        public ushort ReadUInt16()
        {
            ushort output =
                (ushort)(this.FastByte()
                | this.FastByte() << 8);
            return output;
        }

        public short ReadInt16()
        {
            short output =
                (short)(this.FastByte()
                | this.FastByte() << 8);
            return output;
        }

        public uint ReadUInt32()
        {
            uint output = this.FastByte()
                | (uint)this.FastByte() << 8
                | (uint)this.FastByte() << 16
                | (uint)this.FastByte() << 24;

            return output;
        }

        public int ReadInt32()
        {
            int output = this.FastByte()
                | this.FastByte() << 8
                | this.FastByte() << 16
                | this.FastByte() << 24;

            return output;
        }

        public ulong ReadUInt64()
        {
            ulong output = (ulong)this.FastByte()
                | (ulong)this.FastByte() << 8
                | (ulong)this.FastByte() << 16
                | (ulong)this.FastByte() << 24
                | (ulong)this.FastByte() << 32
                | (ulong)this.FastByte() << 40
                | (ulong)this.FastByte() << 48
                | (ulong)this.FastByte() << 56;

            return output;
        }

        public long ReadInt64()
        {
            long output = (long)this.FastByte()
                | (long)this.FastByte() << 8
                | (long)this.FastByte() << 16
                | (long)this.FastByte() << 24
                | (long)this.FastByte() << 32
                | (long)this.FastByte() << 40
                | (long)this.FastByte() << 48
                | (long)this.FastByte() << 56;

            return output;
        }

        public unsafe float ReadSingle()
        {
            float output = 0;
            fixed (byte* bufPtr = &this.Buffer[this.readHead])
            {
                byte* outPtr = (byte*)&output;

                *outPtr = *bufPtr;
                *(outPtr + 1) = *(bufPtr + 1);
                *(outPtr + 2) = *(bufPtr + 2);
                *(outPtr + 3) = *(bufPtr + 3);
            }

            this.Position += 4;
            return output;
        }

        public string ReadString()
        {
            int len = this.ReadPackedInt32();
            if (this.BytesRemaining < len) throw new InvalidDataException($"Read length is longer than message length: {len} of {this.BytesRemaining}");

            string output = UTF8Encoding.UTF8.GetString(this.Buffer, this.readHead, len);

            this.Position += len;
            return output;
        }

        public byte[] ReadBytesAndSize()
        {
            int len = this.ReadPackedInt32();
            if (this.BytesRemaining < len) throw new InvalidDataException($"Read length is longer than message length: {len} of {this.BytesRemaining}");

            return this.ReadBytes(len);
        }

        public byte[] ReadBytes(int length)
        {
            if (this.BytesRemaining < length) throw new InvalidDataException($"Read length is longer than message length: {length} of {this.BytesRemaining}");

            byte[] output = new byte[length];
            Array.Copy(this.Buffer, this.readHead, output, 0, output.Length);
            this.Position += output.Length;
            return output;
        }

        ///
        public int ReadPackedInt32()
        {
            return (int)this.ReadPackedUInt32();
        }

        ///
        public uint ReadPackedUInt32()
        {
            bool readMore = true;
            int shift = 0;
            uint output = 0;

            while (readMore)
            {
                if (this.BytesRemaining < 1) throw new InvalidDataException($"Read length is longer than message length.");

                byte b = this.ReadByte();
                if (b >= 0x80)
                {
                    readMore = true;
                    b ^= 0x80;
                }
                else
                {
                    readMore = false;
                }

                output |= (uint)(b << shift);
                shift += 7;
            }

            return output;
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte FastByte()
        {
            this._position++;
            return this.Buffer[this.readHead++];
        }

        public unsafe static bool IsLittleEndian()
        {
            byte b;
            unsafe
            {
                int i = 1;
                byte* bp = (byte*)&i;
                b = *bp;
            }

            return b == 1;
        }
    }
}
