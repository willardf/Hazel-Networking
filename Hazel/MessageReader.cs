using System;
using System.Text;

namespace Hazel
{
    ///
    public class MessageReader : IRecyclable
    {
        private static readonly ObjectPool<MessageReader> objectPool = new ObjectPool<MessageReader>(() => new MessageReader());

        private byte[] Buffer;
        public byte Tag;
        public int End;

        public int Position;

        public static MessageReader Get(byte[] buffer, int offset, int length)
        {
            var output = objectPool.GetObject();
            output.Buffer = buffer;
            output.Position = offset;
            output.End = length + offset;
            output.Tag = output.ReadByte();

            return output;
        }

        public static MessageReader Get(byte[] buffer, int offset)
        {
            var output = objectPool.GetObject();
            output.Buffer = buffer;
            output.Position = offset;
            output.End = output.ReadUInt16() + offset;
            output.Tag = output.ReadByte();

            return output;
        }

        ///
        public MessageReader ReadMessage()
        {
            var output = MessageReader.Get(this.Buffer, this.Position);
            this.Position += output.End;
            return output;
        }

        ///
        public void Recycle()
        {
            this.Position = this.End = 0;
            objectPool.PutObject(this);
        }

        #region Read Methods
        public bool ReadBoolean()
        {
            byte val = this.Buffer[this.Position++];
            return val != 0;
        }

        public sbyte ReadSByte()
        {
            return (sbyte)this.Buffer[this.Position++];
        }

        public byte ReadByte()
        {
            return this.Buffer[this.Position++];
        }

        public ushort ReadUInt16()
        {
            ushort output = 
                (ushort)(this.Buffer[Position++]
                | this.Buffer[Position++] << 8);
            return output;
        }

        public int ReadInt32()
        {
            int output = this.Buffer[Position++]
                | this.Buffer[Position++] << 8
                | this.Buffer[Position++] << 16
                | this.Buffer[Position++] << 24;

            return output;
        }

        public unsafe float ReadSingle()
        {
            float output = 0;
            fixed (byte* bufPtr = &this.Buffer[this.Position])
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
            string output = UTF8Encoding.UTF8.GetString(this.Buffer, this.Position, len);

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
            Array.Copy(this.Buffer, this.Position, output, 0, output.Length);
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
