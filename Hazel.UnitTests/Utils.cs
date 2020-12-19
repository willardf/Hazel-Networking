using System.Linq;
using System.Text;

namespace Hazel.UnitTests
{
    static class Utils
    {
        /// <summary>
        /// Hex encode a byte array (lower case)
        /// </summary>
        public static string BytesToHex(byte[] data)
        {
            string chars = "0123456789abcdef";

            StringBuilder sb = new StringBuilder(data.Length * 2);
            for (int ii = 0, nn = data.Length; ii != nn; ++ii)
            {
                sb.Append(chars[data[ii] >> 4]);
                sb.Append(chars[data[ii] & 0xF]);
            }

            return sb.ToString().ToLower();
        }

        /// <summary>
        /// Decode a hex string to a byte array (lowercase)
        /// </summary>
        public static byte[] HexToBytes(string hex)
        {
            hex = hex.ToLower();
            hex = hex = string.Concat(hex.Where(c => !char.IsWhiteSpace(c)));

            byte[] output = new byte[hex.Length / 2];

            for (int ii = 0; ii != hex.Length; ++ii)
            {
                byte nibble;

                char c = hex[ii];
                if (c >= 'a')
                {
                    nibble = (byte)(0x0A + c - 'a');
                }
                else
                {
                    nibble = (byte)(c - '0');
                }

                if ((ii & 1) == 0)
                {
                    output[ii / 2] = (byte)(nibble << 4);
                }
                else
                {
                    output[ii / 2] |= nibble;
                }
            }

            return output;
        }
    }
}
