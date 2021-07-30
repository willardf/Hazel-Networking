using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazel.Crypto
{
    /// <summary>
    /// AES encryption interface
    /// </summary>
    public interface IAes : IDisposable
    {
        /// <summary>
        /// Encrypts the specified region of the input byte array and copies
        /// the resulting transform to the specified region of the output
        /// array.
        /// </summary>
        /// <param name="inputSpan">The input for which to encrypt</param>
        /// <param name="outputSpan">
        /// The otput to which to write the encrypted data. This span can
        /// overlap with `inputSpan`.
        /// </param>
        /// <returns>The number of bytes written</returns>
        int EncryptBlock(ByteSpan inputSpan, ByteSpan outputSpan);
    }
}
