using System;
using System.Security.Cryptography;

namespace Hazel.Crypto
{
    public static class SpanCryptoExtensions
    {
        /// <summary>
        /// Clear a span's contents to zero
        /// </summary>
        public static void SecureClear(this ByteSpan span)
        {
            if (span.Length > 0)
            {
                Array.Clear(span.GetUnderlyingArray(), span.Offset, span.Length);
            }
        }

        /// <summary>
        /// Fill a byte span with random data
        /// </summary>
        /// <param name="random">Entropy source</param>
        public static void FillWithRandom(this ByteSpan span, RandomNumberGenerator random)
        {
            if (span.Offset == 0 && span.Length == span.GetUnderlyingArray().Length)
            {
                random.GetBytes(span.GetUnderlyingArray());
                return;
            }

            byte[] temp = new byte[span.Length];
            random.GetBytes(temp);
            new ByteSpan(temp).CopyTo(span);
        }
    }
}
