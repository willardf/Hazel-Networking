﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Hazel
{
    ///
    public class MessageWriter : IRecyclable
    {
        public static int BufferSize = 64000;
        public static readonly ObjectPool<MessageWriter> WriterPool = new ObjectPool<MessageWriter>(() => new MessageWriter(BufferSize));

        public byte[] Buffer;
        public int Length;
        public int Position;

        public SendOption SendOption { get; private set; }

        private Stack<int> messageStarts = new Stack<int>();
        
        public MessageWriter(byte[] buffer)
        {
            this.Buffer = buffer;
            this.Length = this.Buffer.Length;
        }

        ///
        public MessageWriter(int bufferSize)
        {
            this.Buffer = new byte[bufferSize];
        }

        public byte[] ToByteArray(bool includeHeader)
        {
            if (includeHeader)
            {
                byte[] output = new byte[this.Length];
                System.Buffer.BlockCopy(this.Buffer, 0, output, 0, this.Length);
                return output;
            }
            else
            {
                switch (this.SendOption)
                {
                    case SendOption.Reliable:
                        {
                            byte[] output = new byte[this.Length - 3];
                            System.Buffer.BlockCopy(this.Buffer, 3, output, 0, this.Length - 3);
                            return output;
                        }
                    case SendOption.None:
                        {
                            byte[] output = new byte[this.Length - 1];
                            System.Buffer.BlockCopy(this.Buffer, 1, output, 0, this.Length - 1);
                            return output;
                        }
                }
            }

            throw new NotImplementedException();
        }

        ///
        /// <param name="sendOption">The option specifying how the message should be sent.</param>
        public static MessageWriter Get(SendOption sendOption = SendOption.None)
        {
            var output = WriterPool.GetObject();
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
            var messageStart = this.Position;
            messageStarts.Push(messageStart);
            this.Buffer[messageStart] = 0;
            this.Buffer[messageStart + 1] = 0;
            this.Position += 2;
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
            this.Length = this.Position;
        }

        public void Clear(SendOption sendOption)
        {
            Array.Clear(this.Buffer, 0, this.Buffer.Length);
            this.messageStarts.Clear();
            this.SendOption = sendOption;
            this.Buffer[0] = (byte)sendOption;
            switch (sendOption)
            {
                default:
                case SendOption.None:
                    this.Length = this.Position = 1;
                    break;
                case SendOption.Reliable:
                    this.Length = this.Position = 3;
                    break;
            }
        }

        ///
        public void Recycle()
        {
            this.Position = this.Length = 0;
            WriterPool.PutObject(this);
        }

        #region WriteMethods

        public void CopyFrom(MessageReader target)
        {
            int offset, length;
            if (target.Tag == byte.MaxValue)
            {
                offset = target.Offset;
                length = target.Length;
            }
            else
            {
                offset = target.Offset - 3;
                length = target.Length + 3;
            }

            System.Buffer.BlockCopy(target.Buffer, offset, this.Buffer, this.Position, length);
            this.Position += length;
            if (this.Position > this.Length) this.Length = this.Position;
        }

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

        public void Write(uint value)
        {
            this.Buffer[this.Position++] = (byte)value;
            this.Buffer[this.Position++] = (byte)(value >> 8);
            this.Buffer[this.Position++] = (byte)(value >> 16);
            this.Buffer[this.Position++] = (byte)(value >> 24);
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

        public void Write(ulong value)
        {
            this.Buffer[this.Position++] = (byte)value;
            this.Buffer[this.Position++] = (byte)(value >> 8);
            this.Buffer[this.Position++] = (byte)(value >> 16);
            this.Buffer[this.Position++] = (byte)(value >> 24);
            this.Buffer[this.Position++] = (byte)(value >> 32);
            this.Buffer[this.Position++] = (byte)(value >> 40);
            this.Buffer[this.Position++] = (byte)(value >> 48);
            this.Buffer[this.Position++] = (byte)(value >> 56);
            if (this.Position > this.Length) this.Length = this.Position;
        }

        public void Write(long value)
        {
            this.Buffer[this.Position++] = (byte)value;
            this.Buffer[this.Position++] = (byte)(value >> 8);
            this.Buffer[this.Position++] = (byte)(value >> 16);
            this.Buffer[this.Position++] = (byte)(value >> 24);
            this.Buffer[this.Position++] = (byte)(value >> 32);
            this.Buffer[this.Position++] = (byte)(value >> 40);
            this.Buffer[this.Position++] = (byte)(value >> 48);
            this.Buffer[this.Position++] = (byte)(value >> 56);
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

        public void WriteBytesAndSize(byte[] bytes, int offset, int length)
        {
            this.WritePacked((uint)length);
            this.Write(bytes, offset, length);
        }

        public void Write(byte[] bytes)
        {
            Array.Copy(bytes, 0, this.Buffer, this.Position, bytes.Length);
            this.Position += bytes.Length;
            if (this.Position > this.Length) this.Length = this.Position;
        }

        public void Write(byte[] bytes, int offset, int length)
        {
            Array.Copy(bytes, offset, this.Buffer, this.Position, length);
            this.Position += length;
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

        public void Write(MessageWriter msg, bool includeHeader)
        {
            int offset = 0;
            if (!includeHeader)
            {
                switch (msg.SendOption)
                {
                    case SendOption.None:
                        offset = 1;
                        break;
                    case SendOption.Reliable:
                        offset = 3;
                        break;
                }
            }

            this.Write(msg.Buffer, offset, msg.Length - offset);
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
