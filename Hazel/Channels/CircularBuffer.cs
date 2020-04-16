using System;
using System.Collections;
using System.Collections.Generic;

namespace Hazel.Channels
{
    public class CircularBuffer<T> : IEnumerable<T>
    {
        private readonly T[] buffer;

        private int start;
        private int end;
        private int count;

        public CircularBuffer(int capacity)
        {
            this.buffer = new T[capacity];
        }

        public int Capacity => this.buffer.Length;
        public int Count => this.count;

        public bool IsFull => this.count == this.buffer.Length;
        public bool IsEmpty => this.count == 0;

        public T First { get { ThrowIfEmpty(); return this.buffer[start]; } }
        public T Last { get { ThrowIfEmpty(); return this.buffer[(end != 0 ? end : this.buffer.Length) - 1]; } }
        
        public T this[int index]
        {
            get
            {
                int actualIndex = InternalIndex(index);
                return buffer[actualIndex];
            }
            set
            {
                int actualIndex = InternalIndex(index);
                buffer[actualIndex] = value;
            }
        }

        public void Insert(int index, T item)
        {
            int actualIndex = InternalIndex(index);
            T temp = buffer[actualIndex];
            buffer[actualIndex] = item;
            for (int i = index + 1; i < this.count; ++i)
            {
                buffer[InternalIndex(i)] = temp;
            }

            if (!IsFull)
            {
                buffer[end] = temp;
                Increment(ref end);
                ++count;
            }
        }

        public void AddLast(T item)
        {
            if (IsFull)
            {
                buffer[end] = item;
                Increment(ref end);
                start = end;
            }
            else
            {
                buffer[end] = item;
                Increment(ref end);
                ++count;
            }
        }

        public void AddFront(T item)
        {
            if (IsFull)
            {
                Decrement(ref start);
                end = start;
                buffer[start] = item;
            }
            else
            {
                Decrement(ref start);
                buffer[start] = item;
                ++count;
            }
        }

        public void RemoveLast()
        {
            ThrowIfEmpty("Cannot take elements from an empty buffer.");
            Decrement(ref end);
            buffer[end] = default(T);
            --count;
        }

        public void RemoveFirst()
        {
            ThrowIfEmpty("Cannot take elements from an empty buffer.");
            buffer[start] = default(T);
            Increment(ref start);
            --count;
        }

        #region IEnumerable<T> implementation
        public IEnumerator<T> GetEnumerator()
        {
            var segments = new ArraySegment<T>[2] { ArrayOne(), ArrayTwo() };
            foreach (ArraySegment<T> segment in segments)
            {
                for (int i = 0; i < segment.Count; i++)
                {
                    yield return segment.Array[segment.Offset + i];
                }
            }
        }
        #endregion
        #region IEnumerable implementation
        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)GetEnumerator();
        }
        #endregion

        private void ThrowIfEmpty(string message = "Cannot access an empty buffer.")
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Increments the provided index variable by one, wrapping
        /// around if necessary.
        /// </summary>
        /// <param name="index"></param>
        private void Increment(ref int index)
        {
            if (++index == Capacity)
            {
                index = 0;
            }
        }

        /// <summary>
        /// Decrements the provided index variable by one, wrapping
        /// around if necessary.
        /// </summary>
        /// <param name="index"></param>
        private void Decrement(ref int index)
        {
            if (index == 0)
            {
                index = Capacity;
            }
            index--;
        }

        /// <summary>
        /// Converts the index in the argument to an index in <code>_buffer</code>
        /// </summary>
        /// <returns>
        /// The transformed index.
        /// </returns>
        /// <param name='index'>
        /// External index.
        /// </param>
        private int InternalIndex(int index)
        {
            return start + (index < (Capacity - start) ? index : index - Capacity);
        }

        // doing ArrayOne and ArrayTwo methods returning ArraySegment<T> as seen here: 
        // http://www.boost.org/doc/libs/1_37_0/libs/circular_buffer/doc/circular_buffer.html#classboost_1_1circular__buffer_1957cccdcb0c4ef7d80a34a990065818d
        // http://www.boost.org/doc/libs/1_37_0/libs/circular_buffer/doc/circular_buffer.html#classboost_1_1circular__buffer_1f5081a54afbc2dfc1a7fb20329df7d5b
        // should help a lot with the code.

        #region Array items easy access.
        // The array is composed by at most two non-contiguous segments, 
        // the next two methods allow easy access to those.

        private ArraySegment<T> ArrayOne()
        {
            if (IsEmpty)
            {
                return new ArraySegment<T>(new T[0]);
            }
            else if (start < end)
            {
                return new ArraySegment<T>(buffer, start, end - start);
            }
            else
            {
                return new ArraySegment<T>(buffer, start, buffer.Length - start);
            }
        }

        private ArraySegment<T> ArrayTwo()
        {
            if (IsEmpty)
            {
                return new ArraySegment<T>(new T[0]);
            }
            else if (start < end)
            {
                return new ArraySegment<T>(buffer, end, 0);
            }
            else
            {
                return new ArraySegment<T>(buffer, 0, end);
            }
        }
        #endregion
    }
}