using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Hazel
{
    ///
    public class MessageWriter : IRecyclable
    {
        public static int BufferSize = 64000;
        private static readonly ObjectPool<MessageWriter> objectPool = new ObjectPool<MessageWriter>(() => new MessageWriter(BufferSize));

        internal byte[] Buffer;
        public int Length;
        public int Position;

        public SendOption SendOption { get; private set; }

        private Stack<int> messageStarts = new Stack<int>();
        
        ///
        public MessageWriter(int bufferSize)
        {
            this.Buffer = new byte[bufferSize];
        }

        ///
        /// <param name="sendOption">The option specifying how the message should be sent.</param>
        public static MessageWriter Get(SendOption sendOption = SendOption.None)
        {
            var output = objectPool.GetObject();
            output.Clear(sendOption);

            return output;
        }

        public bool HasBytes(int expected)
        {
            if (this.SendOption == SendOption.None)
            {
                return this.Length > 1 + expected;
            }

            return this.Length > 3 + expected;
        }

        ///
        public void StartMessage(byte typeFlag)
        {
            messageStarts.Push(this.Position);
            this.Position += 2; // Skip for size
            this.Write(typeFlag);
        }

        ///
        public void EndMessage()
        {
            var lastMessageStart = messageStarts.Pop();
            ushort length = (ushort)(this.Position - lastMessageStart - 3); // Minus length and type byte
            this.Buffer[lastMessageStart] = (byte)length;
            this.Buffer[lastMessageStart + 1] = (byte)(length >> 8);
        }

        ///
        public void CancelMessage()
        {
            this.Position = this.messageStarts.Pop();
        }

        public void Clear(SendOption sendOption)
        {
            this.Position = this.Length = 0;
            this.SendOption = sendOption;

            this.Buffer[0] = (byte)sendOption;
            switch (sendOption)
            {
                case SendOption.None:
                    this.Length = this.Position = 1;
                    break;
                case SendOption.Reliable:
                    this.Length = this.Position = 3;
                    break;
                case SendOption.FragmentedReliable:
                    throw new NotImplementedException("Sry bruh");
            }
        }

        ///
        public void Recycle()
        {
            this.Position = this.Length = 0;
            objectPool.PutObject(this);
        }

        #region WriteMethods
        public void Write(bool value)
        {
            this.Buffer[this.Position++] = (byte)(value ? 1 : 0);
            if (this.Position > this.Length) this.Length = this.Position;
        }

        public void Write(sbyte value)
        {
            this.Buffer[this.Position++] = (byte)value;
            if (this.Position > this.Length) this.Length = this.Position;
        }

        public void Write(byte value)
        {
            this.Buffer[this.Position++] = value;
            if (this.Position > this.Length) this.Length = this.Position;
        }

        public void Write(short value)
        {
            this.Buffer[this.Position++] = (byte)value;
            this.Buffer[this.Position++] = (byte)(value >> 8);
            if (this.Position > this.Length) this.Length = this.Position;
        }

        public void Write(ushort value)
        {
            this.Buffer[this.Position++] = (byte)value;
            this.Buffer[this.Position++] = (byte)(value >> 8);
            if (this.Position > this.Length) this.Length = this.Position;
        }

        public void Write(int value)
        {
            this.Buffer[this.Position++] = (byte)value;
            this.Buffer[this.Position++] = (byte)(value >> 8);
            this.Buffer[this.Position++] = (byte)(value >> 16);
            this.Buffer[this.Position++] = (byte)(value >> 24);
            if (this.Position > this.Length) this.Length = this.Position;
        }

        public unsafe void Write(float value)
        {
            fixed (byte* ptr = &this.Buffer[this.Position])
            {
                byte* valuePtr = (byte*)&value;

                *ptr = *valuePtr;
                *(ptr + 1) = *(valuePtr + 1);
                *(ptr + 2) = *(valuePtr + 2);
                *(ptr + 3) = *(valuePtr + 3);
            }

            this.Position += 4;
            if (this.Position > this.Length) this.Length = this.Position;
        }

        public void Write(string value)
        {
            var bytes = UTF8Encoding.UTF8.GetBytes(value);
            this.WritePacked(bytes.Length);
            this.Write(bytes);
        }

        public void WriteBytesAndSize(byte[] bytes)
        {
            this.WritePacked((uint)bytes.Length);
            this.Write(bytes);
        }

        public void WriteBytesAndSize(byte[] bytes, int length)
        {
            this.WritePacked((uint)length);
            this.Write(bytes, length);
        }

        public void Write(byte[] bytes)
        {
            Array.Copy(bytes, 0, this.Buffer, this.Position, bytes.Length);
            this.Position += bytes.Length;
            if (this.Position > this.Length) this.Length = this.Position;
        }

        public void Write(byte[] bytes, int length)
        {
            Array.Copy(bytes, 0, this.Buffer, this.Position, length);
            this.Position += length;
            if (this.Position > this.Length) this.Length = this.Position;
        }

        ///
        public void WritePacked(int value)
        {
            this.WritePacked((uint)value);
        }

        ///
        public void WritePacked(uint value)
        {
            do
            {
                byte b = (byte)(value & 0xFF);
                if (value >= 0x80)
                {
                    b |= 0x80;
                }

                this.Write(b);
                value >>= 7;
            } while (value > 0);
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
