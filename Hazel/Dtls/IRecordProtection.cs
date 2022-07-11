using System;

namespace Hazel.Dtls
{
    /// <summary>
    /// DTLS cipher suite interface for protection of record payload.
    /// </summary>
    public interface IRecordProtection : IDisposable
    {
        /// <summary>
        /// Calculate the size of an encrypted plaintext
        /// </summary>
        /// <param name="dataSize">Size of plaintext in bytes</param>
        /// <returns>Size of encrypted ciphertext in bytes</returns>
        int GetEncryptedSize(int dataSize);

        /// <summary>
        /// Calculate the size of decrypted ciphertext
        /// </summary>
        /// <param name="dataSize">Size of ciphertext in bytes</param>
        /// <returns>Size of decrypted plaintext in bytes</returns>
        int GetDecryptedSize(int dataSize);

        /// <summary>
        /// Encrypt a plaintext intput with server keys
        ///
        /// Output may overlap with input.
        /// </summary>
        /// <param name="output">Output ciphertext</param>
        /// <param name="input">Input plaintext</param>
        /// <param name="record">Parent DTLS record</param>
        void EncryptServerPlaintext(ByteSpan output, ByteSpan input, ref Record record);

        /// <summary>
        /// Encrypt a plaintext intput with client keys
        ///
        /// Output may overlap with input.
        /// </summary>
        /// <param name="output">Output ciphertext</param>
        /// <param name="input">Input plaintext</param>
        /// <param name="record">Parent DTLS record</param>
        void EncryptClientPlaintext(ByteSpan output, ByteSpan input, ref Record record);

        /// <summary>
        /// Decrypt a ciphertext intput with server keys
        ///
        /// Output may overlap with input.
        /// </summary>
        /// <param name="output">Output plaintext</param>
        /// <param name="input">Input ciphertext</param>
        /// <param name="record">Parent DTLS record</param>
        /// <returns>True if the input was authenticated and decrypted. Otherwise false</returns>
        bool DecryptCiphertextFromServer(ByteSpan output, ByteSpan input, ref Record record);

        /// <summary>
        /// Decrypt a ciphertext intput with client keys
        ///
        /// Output may overlap with input.
        /// </summary>
        /// <param name="output">Output plaintext</param>
        /// <param name="input">Input ciphertext</param>
        /// <param name="record">Parent DTLS record</param>
        /// <returns>True if the input was authenticated and decrypted. Otherwise false</returns>
        bool DecryptCiphertextFromClient(ByteSpan output, ByteSpan input, ref Record record);
    }

    /// <summary>
    /// Factory to create record protection from cipher suite identifiers
    /// </summary>
    public sealed class RecordProtectionFactory
    {
        public static IRecordProtection Create(CipherSuite cipherSuite, ByteSpan masterSecret, ByteSpan serverRandom, ByteSpan clientRandom)
        {
            switch (cipherSuite)
            {
            case CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256:
                return new Aes128GcmRecordProtection(masterSecret, serverRandom, clientRandom);

            default:
                return null;
            }
        }
    }
}
