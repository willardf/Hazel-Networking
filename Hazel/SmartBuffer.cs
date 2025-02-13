using System;
using System.Threading;

namespace Hazel
{
    public class SmartBuffer : IRecyclable, IDisposable
    {
        private readonly ObjectPool<SmartBuffer> parent;

        public byte[] Buffer;
        private int usageCount;

        private int length;
        public int Length
        {
            get => this.length;
            set
            {
                this.length = value;

                if (value > this.Buffer.Length)
                {
                    this.Buffer = new byte[value];
                }
            }
        }

        public SmartBuffer(ObjectPool<SmartBuffer> parent, int size)
        {
            this.parent = parent;
            this.Buffer = new byte[size];
            this.usageCount = 1;
        }

        public byte this[int i]
        {
            get => this.Buffer[i];
            set => this.Buffer[i] = value;
        }


        public static explicit operator ByteSpan(SmartBuffer b)
        {
            return new ByteSpan(b.Buffer, 0, b.length);
        }

        public static explicit operator byte[](SmartBuffer b)
        {
            return b.Buffer;
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
            System.Buffer.BlockCopy(bytes, 0, this.Buffer, 0, bytes.Length);
        }

        public void CopyFrom(MessageWriter data, bool includeHeader = true)
        {
            int offset = 0;
            if (!includeHeader)
            {
                switch (data.SendOption)
                {
                    case SendOption.None:
                        offset = 1;
                        break;
                    case SendOption.Reliable:
                        offset = 3;
                        break;
                }
            }

            this.Length = data.Length - offset;
            System.Buffer.BlockCopy(data.Buffer, offset, this.Buffer, 0, this.Length);
        }
    }
}
