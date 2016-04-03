using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel
{
    class Utility
    {
        /// <summary>
        ///     Appends the length header to the bytes.
        /// </summary>
        /// <param name="bytes">The source bytes.</param>
        /// <returns></returns>
        internal static byte[] AppendLengthHeader(byte[] bytes)
        {
            byte[] fullBytes = new byte[bytes.Length + 4];

            //Append length
            fullBytes[0] = (byte)(((uint)bytes.Length >> 24) & 0xFF);
            fullBytes[1] = (byte)(((uint)bytes.Length >> 16) & 0xFF);
            fullBytes[2] = (byte)(((uint)bytes.Length >> 8) & 0xFF);
            fullBytes[3] = (byte)(uint)bytes.Length;

            //Add rest of bytes
            Buffer.BlockCopy(bytes, 0, fullBytes, 4, bytes.Length);

            return fullBytes;
        }

        /// <summary>
        ///     Returns the length from a length header.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        internal static int GetLengthFromBytes(byte[] bytes)
        {
            if (bytes.Length < 4)
                throw new IndexOutOfRangeException("Not enough bytes passed to calculate length.");

            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }
    }
}
