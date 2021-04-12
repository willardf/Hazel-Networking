using System;
using System.IO;
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

        private MemoryStream innerStream = new MemoryStream();

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
            if  (this.innerStream != null)
            {
                this.Reset();
                this.innerStream.Dispose();
                this.innerStream = null;
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Reset the stream to its initial state
        /// </summary>
        public void Reset()
        {
            ByteSpan buffer = this.innerStream.GetBuffer();
            buffer.SecureClear();

            this.innerStream.SetLength(0);
        }

        /// <summary>
        /// Add data to the stream
        /// </summary>
        public void AddData(ByteSpan data)
        {
            this.innerStream.Write(data.GetUnderlyingArray(), data.Offset, data.Length);
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

            using (SHA256 sha256 = SHA256.Create())
            {
                this.innerStream.Position = 0;
                ByteSpan digest = sha256.ComputeHash(this.innerStream);
                digest.CopyTo(output);
            }
        }
    }
}
