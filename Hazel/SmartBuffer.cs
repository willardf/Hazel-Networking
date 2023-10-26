using System;
using System.Threading;

namespace Hazel
{
    public class SmartBuffer : IRecyclable, IDisposable
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
            this.usageCount = 1;
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

        public void Dispose()
        {
            this.Recycle();
        }

        public void Recycle()
        {
            int lockValue = Interlocked.Decrement(ref this.usageCount);
            if (lockValue == 0)
            {
                // I had to think about if this is safe and it is.
                // If I'm in here, then I am the last one out the door.
                // No one can come after me until PutObject/GetObject are called
                this.usageCount = 1;
                parent.PutObject(this);
            }
            else if (lockValue < 0)
            {
                throw new HazelException("UH OH! SmartBuffer was used without calling AddUsage?");
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
