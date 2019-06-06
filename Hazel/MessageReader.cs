using System;
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

            output.Offset = 0;
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
            var output = GetSized(source.Buffer.Length);
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

        ///
        public MessageReader ReadMessage()
        {
            // Ensure there is at least a header
            if (this.readHead + 3 > this.Buffer.Length) return null;

            var output = new MessageReader();

            output.Buffer = this.Buffer;
            output.Offset = this.readHead;
            output.Position = 0;

            output.Length = output.ReadUInt16();
            output.Tag = output.ReadByte();

            output.Offset += 3;
            output.Position = 0;

            this.Position += output.Length + 3;
            return output;
        }

        ///
        public void Recycle()
        {
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
            string output = UTF8Encoding.UTF8.GetString(this.Buffer, this.readHead, len);

            this.Position += len;
            return output;
        }

        public byte[] ReadBytesAndSize()
        {
            int len = this.ReadPackedInt32();
            return this.ReadBytes(len);
        }

        public byte[] ReadBytes(int length)
        {
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
