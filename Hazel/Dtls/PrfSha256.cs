using System.Text;
using System.Security.Cryptography;

namespace Hazel.Dtls
{
    /// <summary>
    /// Common Psuedorandom Function labels for TLS
    /// </summary>
    public struct PrfLabel
    {
        public static readonly ByteSpan MASTER_SECRET = LabelToBytes("master secert");
        public static readonly ByteSpan KEY_EXPANSION = LabelToBytes("key expansion");
        public static readonly ByteSpan CLIENT_FINISHED = LabelToBytes("client finished");
        public static readonly ByteSpan SERVER_FINISHED = LabelToBytes("server finished");

        /// <summary>
        /// Convert a text label to a byte sequence
        /// </summary>
        private static ByteSpan LabelToBytes(string label)
        {
            return Encoding.ASCII.GetBytes(label);
        }
    }

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
        public static void ExpandSecret(ObjectPool<SmartBuffer> bufferPool, ByteSpan output, ByteSpan key, string label, ByteSpan initialSeed)
        {
            using SmartBuffer labelBuffer = bufferPool.GetObject();
            labelBuffer.Length = Encoding.ASCII.GetBytes(label, 0, label.Length, (byte[])labelBuffer, 0);

            ExpandSecret(bufferPool, output, key, (ByteSpan)labelBuffer, initialSeed);
        }

        /// <summary>
        /// Expand a secret key
        /// </summary>
        /// <param name="output">Output span. Length determines how much data to generate</param>
        /// <param name="key">Original key to expand</param>
        /// <param name="label">Label (treated as a salt)</param>
        /// <param name="initialSeed">Seed for expansion (treated as a salt)</param>
        public static void ExpandSecret(ObjectPool<SmartBuffer> bufferPool, ByteSpan output, ByteSpan key, ByteSpan label, ByteSpan initialSeed)
        {
            ByteSpan writer = output;

            using SmartBuffer roundSeedBuffer = bufferPool.GetObject();
            roundSeedBuffer.Length = label.Length + initialSeed.Length;

            ByteSpan roundSeed = (ByteSpan)roundSeedBuffer;
            label.CopyTo(roundSeed);
            initialSeed.CopyTo(roundSeed.Slice(label.Length));

            ByteSpan hashA = roundSeed;

            using (HMACSHA256 hmac = new HMACSHA256(key.ToArray()))
            {
                using SmartBuffer inputBuffer = bufferPool.GetObject();
                inputBuffer.Length = hmac.OutputBlockSize + roundSeed.Length;

                ByteSpan input = (ByteSpan)inputBuffer;
                roundSeed.CopyTo(input.Slice(hmac.OutputBlockSize));

                while (writer.Length > 0)
                {
                    // Update hashA
                    hashA = hmac.ComputeHash(hashA.GetUnderlyingArray(), hashA.Offset, hashA.Length);

                    // generate hash input
                    hashA.CopyTo(input);

                    ByteSpan roundOutput = hmac.ComputeHash(input.GetUnderlyingArray(), input.Offset, input.Length);
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
