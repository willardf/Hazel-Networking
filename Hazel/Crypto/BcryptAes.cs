using System;
using System.Runtime.InteropServices;

namespace Hazel.Crypto
{
    /// <summary>
    /// AES implementation using he native Windows Bcrypt API
    /// </summary>
    public class Bcrypt32Aes : IAes
    {
        private const string DllName = "Hazel.Aes.Bcrypt.32.dll";

        private IntPtr m_context = IntPtr.Zero;

        [DllImport(DllName)]
        private static extern IntPtr AesBrypt_create(byte[] keyArray, int keyArrayOffset, int keyArrayLength);

        [DllImport(DllName)]
        private static extern void AesBcrypt_release(IntPtr context);

        [DllImport(DllName)]
        private static extern int AesBcrypt_encrpytBlock(IntPtr context, byte[] inputArray, int inputOffset, int inputLength, byte[] outputArray, int outputOffset);

        /// <summary>
        /// Create a new instance of the Bcrypt AES implementation
        /// </summary>
        /// <param name="key">Encryption key</param>
        public Bcrypt32Aes(ByteSpan key)
        {
            this.m_context = AesBrypt_create(key.GetUnderlyingArray(), key.Offset, key.Length);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            AesBcrypt_release(this.m_context);
            this.m_context = IntPtr.Zero;
        }

        /// <inheritdoc/>
        public int EncryptBlock(ByteSpan inputSpan, ByteSpan outputSpan)
        {
            if (inputSpan.Length != outputSpan.Length)
            {
                throw new ArgumentException($"ouputSpan length ({outputSpan.Length}) does not match inputSpan length ({inputSpan.Length})", nameof(outputSpan));
            }

            return AesBcrypt_encrpytBlock(this.m_context, inputSpan.GetUnderlyingArray(), inputSpan.Offset, inputSpan.Length, outputSpan.GetUnderlyingArray(), outputSpan.Offset);
        }
    }

    /// <summary>
    /// AES implementation using he native Windows Bcrypt API
    /// </summary>
    public class Bcrypt64Aes : IAes
    {
        private const string DllName = "Hazel.Aes.Bcrypt.64.dll";

        private IntPtr m_context = IntPtr.Zero;

        [DllImport(DllName)]
        private static extern IntPtr AesBrypt_create(byte[] keyArray, int keyArrayOffset, int keyArrayLength);

        [DllImport(DllName)]
        private static extern void AesBcrypt_release(IntPtr context);

        [DllImport(DllName)]
        private static extern int AesBcrypt_encrpytBlock(IntPtr context, byte[] inputArray, int inputOffset, int inputLength, byte[] outputArray, int outputOffset);

        /// <summary>
        /// Create a new instance of the Bcrypt AES implementation
        /// </summary>
        /// <param name="key">Encryption key</param>
        public Bcrypt64Aes(ByteSpan key)
        {
            this.m_context = AesBrypt_create(key.GetUnderlyingArray(), key.Offset, key.Length);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            AesBcrypt_release(this.m_context);
            this.m_context = IntPtr.Zero;
        }

        /// <inheritdoc/>
        public int EncryptBlock(ByteSpan inputSpan, ByteSpan outputSpan)
        {
            if (inputSpan.Length != outputSpan.Length)
            {
                throw new ArgumentException($"ouputSpan length ({outputSpan.Length}) does not match inputSpan length ({inputSpan.Length})", nameof(outputSpan));
            }

            return AesBcrypt_encrpytBlock(this.m_context, inputSpan.GetUnderlyingArray(), inputSpan.Offset, inputSpan.Length, outputSpan.GetUnderlyingArray(), outputSpan.Offset);
        }
    }
}
