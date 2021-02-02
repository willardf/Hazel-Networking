using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;

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

        public static byte[] DecodePEM(string pemData)
        {
            List<byte> result = new List<byte>();

            pemData = pemData.Replace("\r", "");
            string[] lines = pemData.Split('\n');
            foreach (string line in lines)
            {
                if (line.StartsWith("-----"))
                {
                    continue;
                }

                byte[] lineData = Convert.FromBase64String(line);
                result.AddRange(lineData);
            }

            return result.ToArray();
        }

        public static RSA DecodeRSAKeyFromPEM(string pemData)
        {
            PemReader pemReader = new PemReader(new StringReader(pemData));
            RsaPrivateCrtKeyParameters keyParameters = (RsaPrivateCrtKeyParameters)pemReader.ReadObject();
            return DotNetUtilities.ToRSA(keyParameters);
        }
    }
}
