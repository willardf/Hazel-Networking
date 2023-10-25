using System;
using System.Threading;

namespace Hazel
{
    public class SmartBuffer : IRecyclable
    {
        private readonly ObjectPool<SmartBuffer> parent;

        private byte[] buffer;
        private int usageCount;

        private int length;
        public int Length
        {
            get => this.length;
            set
            {
                this.length = value;

                if (value > this.buffer.Length)
                {
                    this.buffer = new byte[value];
                }
            }
        }

        public SmartBuffer(ObjectPool<SmartBuffer> parent, int size)
        {
            this.parent = parent;
            this.buffer = new byte[size];
        }

        public byte this[int i]
        {
            get => this.buffer[i];
            set => this.buffer[i] = value;
        }


        public static explicit operator ByteSpan(SmartBuffer b)
        {
            return new ByteSpan(b.buffer, 0, b.length);
        }

        public static explicit operator byte[](SmartBuffer b)
        {
            return b.buffer;
        }

        public void AddUsage()
        {
            Interlocked.Increment(ref this.usageCount);
        }

        public void Recycle()
        {
            if (Interlocked.Decrement(ref this.usageCount) == 0)
            {
                parent.PutObject(this);
            }
        }

        public void CopyFrom(byte[] bytes)
        {
            this.Length = bytes.Length;
            Buffer.BlockCopy(bytes, 0, this.buffer, 0, bytes.Length);
        }

        public void CopyFrom(MessageWriter data)
        {
            this.Length = data.Length;
            Buffer.BlockCopy(data.Buffer, 0, this.buffer, 0, data.Length);
        }
    }
}
