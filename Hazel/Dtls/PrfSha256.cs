using System.Text;
using System.Security.Cryptography;

namespace Hazel.Dtls
{
    /// <summary>
    /// The P_SHA256 Psuedorandom Function
    /// </summary>
    public struct PrfSha256
    {
        /// <summary>
        /// Expand a secret key
        /// </summary>
        /// <param name="output">Output span. Length determines how much data to generate</param>
        /// <param name="key">Original key to expand</param>
        /// <param name="label">Label (treated as a salt)</param>
        /// <param name="initialSeed">Seed for expansion (treated as a salt)</param>
        public static void ExpandSecret(ByteSpan output, ByteSpan key, string label, ByteSpan initialSeed)
        {
            ByteSpan writer = output;

            ///TODO(mendsley): Look into pre-calculating all of these
            ByteSpan labelAsBytes = Encoding.ASCII.GetBytes(label);

            byte[] roundSeed = new byte[labelAsBytes.Length + initialSeed.Length];
            labelAsBytes.CopyTo(roundSeed);
            initialSeed.CopyTo(roundSeed, labelAsBytes.Length));

            byte[] hashA = roundSeed;

            using (HMACSHA256 hmac = new HMACSHA256(key.ToArray()))
            {
                byte[] input = new byte[hmac.OutputBlockSize + roundSeed.Length];
                new ByteSpan(roundSeed).CopyTo(input, hmac.OutputBlockSize);

                while (writer.Length > 0)
                {
                    // Update hashA
                    hashA = hmac.ComputeHash(hashA);

                    // generate hash input
                    new ByteSpan(hashA).CopyTo(input);

                    ByteSpan roundOutput = hmac.ComputeHash(input);
                    if (roundOutput.Length > writer.Length)
                    {
                        roundOutput = roundOutput.Slice(0, writer.Length);
                    }

                    roundOutput.CopyTo(writer);
                    writer = writer.Slice(roundOutput.Length);
                }
            }
        }
    }
}
