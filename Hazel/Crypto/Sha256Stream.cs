using System;
using System.Security.Cryptography;

namespace Hazel.Crypto
{
    /// <summary>
    /// Streams data into a SHA256 digest
    /// </summary>
    public class Sha256Stream : IDisposable
    {
        /// <summary>
        /// Size of the SHA256 digest in bytes
        /// </summary>
        public const int DigestSize = 32;

        private SHA256 hash = SHA256.Create();

        struct EmptyArray
        {
            public static readonly byte[] Value = new byte[0];
        }

        /// <summary>
        /// Create a new instance of a SHA256 stream
        /// </summary>
        public Sha256Stream()
        {
        }

        /// <summary>
        /// Release resources associated with the stream
        /// </summary>
        public void Dispose()
        {
            this.hash?.Dispose();
            this.hash = null;

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Reset the stream to its initial state
        /// </summary>
        public void Reset()
        {
            this.hash?.Dispose();
            this.hash = SHA256.Create();
        }

        /// <summary>
        /// Add data to the stream
        /// </summary>
        public void AddData(ByteSpan data)
        {
            while (data.Length > 0)
            {
                int offset = this.hash.TransformBlock(data.GetUnderlyingArray(), data.Offset, data.Length, null, 0);
                data = data.Slice(offset);
            }
        }

        /// <summary>
        /// Calculate the final hash of the stream data
        /// </summary>
        /// <param name="output">
        /// Target span to which the hash will be written
        /// </param>
        public void CalculateHash(ByteSpan output)
        {
            if (output.Length != DigestSize)
            {
                throw new ArgumentException($"Expected a span of {DigestSize} bytes. Got a span of {output.Length} bytes", nameof(output));
            }

            this.hash.TransformFinalBlock(EmptyArray.Value, 0, 0);
            new ByteSpan(this.hash.Hash).CopyTo(output);
        }
    }
}
