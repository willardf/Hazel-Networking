using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel.Dtls
{
    /// <summary>
    /// Passthrough record protection implementaion
    /// </summary>
    public class NullRecordProtection : IRecordProtection
    {
        public readonly static NullRecordProtection Instance = new NullRecordProtection();

        public void Dispose()
        {
        }

        public int GetEncryptedSize(int dataSize)
        {
            return dataSize;
        }

        public int GetDecryptedSize(int dataSize)
        {
            return dataSize;
        }

        public void EncryptServerPlaintext(ByteSpan output, ByteSpan input, ref Record record)
        {
            CopyMaybeOverlappingSpans(output, input);
        }

        public void EncryptClientPlaintext(ByteSpan output, ByteSpan input, ref Record record)
        {
            CopyMaybeOverlappingSpans(output, input);
        }

        public bool DecryptCiphertextFromServer(ByteSpan output, ByteSpan input, ref Record record)
        {
            CopyMaybeOverlappingSpans(output, input);
            return true;
        }

        public bool DecryptCiphertextFromClient(ByteSpan output, ByteSpan input, ref Record record)
        {
            CopyMaybeOverlappingSpans(output, input);
            return true;
        }

        private static void CopyMaybeOverlappingSpans(ByteSpan output, ByteSpan input)
        {
            // Early out if the ranges `output` is equal to `input`
            if (output.GetUnderlyingArray() == input.GetUnderlyingArray())
            {
                if (output.Offset == input.Offset && output.Length == input.Length)
                {
                    return;
                }
            }

            input.CopyTo(output);
        }
    }
}
