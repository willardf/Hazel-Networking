using System;

namespace Hazel
{
    /// <summary>
    /// This is a minimal implementation of `System.Span` in .NET 5.0
    /// </summary>
    public struct ByteSpan
    {
        private readonly byte[] array_;

        /// <summary>
        /// Createa a new span object containing an entire array
        /// </summary>
        public ByteSpan(byte[] array)
        {
            if (array == null)
            {
                this.array_ = null;
                this.Offset = 0;
                this.Length = 0;
            }
            else
            {
                this.array_ = array;
                this.Offset = 0;
                this.Length = array.Length;
            }
        }

        /// <summary>
        /// Creates a new span object containing a subset of an array
        /// </summary>
        public ByteSpan(byte[] array, int offset, int length)
        {
            if (array == null)
            {
                if (offset != 0)
                {
                    throw new ArgumentException("Invalid offset", nameof(offset));
                }
                if (length != 0)
                {
                    throw new ArgumentException("Invalid length", nameof(offset));
                }

                this.array_ = null;
                this.Offset = 0;
                this.Length = 0;
            }
            else
            {
                if (offset < 0 || offset > array.Length)
                {
                    throw new ArgumentException("Invalid offset", nameof(offset));
                }
                if (length < 0)
                {
                    throw new ArgumentException($"Invalid length: {length}", nameof(length));
                }
                if ((offset + length) > array.Length)
                {
                    throw new ArgumentException($"Invalid length. Length: {length} Offset: {offset} Array size: {array.Length}", nameof(length));
                }

                this.array_ = array;
                this.Offset = offset;
                this.Length = length;
            }
        }

        /// <summary>
        /// Returns the underlying array.
        ///
        /// WARNING: This does not return the span, but the entire underlying storage block
        /// </summary>
        public byte[] GetUnderlyingArray()
        {
            return this.array_;
        }

        /// <summary>
        /// Returns the offset into the underlying array
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Returns the length of the current span
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the span element at the specified index
        /// </summary>
        public byte this[int index]
        {
            get
            {
                if (index < 0 || index >= this.Length)
                {
                    throw new IndexOutOfRangeException();
                }

                return this.array_[this.Offset + index];
            }
            set
            {
                if (index < 0 || index >= this.Length)
                {
                    throw new IndexOutOfRangeException();
                }

                this.array_[this.Offset + index] = value;
            }
        }

        /// <summary>
        /// Create a new span that is a subset of this span [offset, this.Length-offset)
        /// </summary>
        public ByteSpan Slice(int offset)
        {
            return Slice(offset, this.Length - offset);
        }

        /// <summary>
        /// Create a new span that is a subset of this span [offset, length)
        /// </summary>
        public ByteSpan Slice(int offset, int length)
        {
            return new ByteSpan(this.array_, this.Offset + offset, length);
        }

        /// <summary>
        /// Copies the contents of the span to an array
        /// </summary>
        public void CopyTo(byte[] array, int offset)
        {
            CopyTo(new ByteSpan(array, offset, array.Length - offset));
        }

        /// <summary>
        /// Copies the contents of the span to another span
        /// </summary>
        public void CopyTo(ByteSpan destination)
        {
            if (destination.Length < this.Length)
            {
                throw new ArgumentException("Destination span is shorter than source", nameof(destination));
            }

            if (Length > 0)
            {
                Buffer.BlockCopy(this.array_, this.Offset, destination.array_, destination.Offset, this.Length);
            }
        }

        /// <summary>
        /// Create a new array with the contents of this span
        /// </summary>
        public byte[] ToArray()
        {
            byte[] result = new byte[Length];
            CopyTo(result);
            return result;
        }

        /// <summary>
        /// Implicit conversion from byte[] -> ByteSpan
        /// </summary>
        public static implicit operator ByteSpan(byte[] array)
        {
            return new ByteSpan(array);
        }

        /// <summary>
        /// Retuns an empty span object
        /// </summary>
        public static ByteSpan Empty
        {
            get
            {
                return new ByteSpan(null);
            }
        }
    }
}
