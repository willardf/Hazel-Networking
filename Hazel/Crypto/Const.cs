using System.Diagnostics;

namespace Hazel.Crypto
{
    public static class Const
    {

        /// <summary>
        /// Compare two bytes for equality.
        ///
        /// This takes care to always use a constant amount of time to prevent
        /// leaking information through side-channel attacks.
        ///
        /// This is aceived by collapsing the xor bits down into a single bit.
        ///
        /// Ported from:
        /// https://github.com/mendsley/tiny/blob/master/include/tiny/crypto/constant.h
        /// </summary>
        /// <returns>
        /// Returns `1` is the two bytes or equivalent. Otherwise, returns `0`
        /// </returns>
        public static byte ConstantCompareByte(byte a, byte b)
        {
            byte result = (byte)(~(a ^ b));

            // collapse bits down to the LSB
            result &= (byte)(result >> 4);
            result &= (byte)(result >> 2);
            result &= (byte)(result >> 1);

            return result;
        }

        /// <summary>
        /// Compare two equal length spans for equality.
        ///
        /// This takes care to always use a constant amount of time to prevent
        /// leaking information through side-channel attacks.
        ///
        /// Ported from:
        /// https://github.com/mendsley/tiny/blob/master/include/tiny/crypto/constant.h
        /// </summary>
        /// <returns>
        /// Returns `1` if the spans are equivalent. Others, returns `0`.
        /// </returns>
        public static byte ConstantCompareSpans(ByteSpan a, ByteSpan b)
        {
            Debug.Assert(a.Length == b.Length);

            byte value = 0;
            for (int ii = 0, nn = a.Length; ii != nn; ++ii)
            {
                value |= (byte)(a[ii] ^ b[ii]);
            }

            return ConstantCompareByte(value, 0);
        }

        /// <summary>
        /// Compare a span against an all zero span
        ///
        /// This takes care to always use a constant amount of time to prevent
        /// leaking information through side-channel attacks.
        ///
        /// Ported from:
        /// https://github.com/mendsley/tiny/blob/master/include/tiny/crypto/constant.h
        /// </summary>
        /// <returns>
        /// Returns `1` if the spans is all zeros. Others, returns `0`.
        /// </returns>
        public static byte ConstantCompareZeroSpan(ByteSpan a)
        {
            byte value = 0;
            for (int ii = 0, nn = a.Length; ii != nn; ++ii)
            {
                value |= (byte)(a[ii] ^ 0);
            }

            return ConstantCompareByte(value, 0);
        }
    }
}
