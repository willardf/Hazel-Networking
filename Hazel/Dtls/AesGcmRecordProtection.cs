using Hazel.Crypto;
using System;
using System.Diagnostics;

namespace Hazel.Dtls
{
    /// <summary>
    /// *_AES_128_GCM_* cipher suite
    /// </summary>
    public class Aes128GcmRecordProtection: IRecordProtection
    {
        private const int ImplicitNonceSize = 4;
        private const int ExplicitNonceSize = 8;

        private readonly ObjectPool<SmartBuffer> bufferPool;

        private readonly Aes128Gcm serverWriteCipher;
        private readonly Aes128Gcm clientWriteCipher;

        private readonly ByteSpan serverWriteIV;
        private readonly ByteSpan clientWriteIV;

        /// <summary>
        /// Create a new instance of the AES128_GCM record protection
        /// </summary>
        /// <param name="masterSecret">Shared secret</param>
        /// <param name="serverRandom">Server random data</param>
        /// <param name="clientRandom">Client random data</param>
        public Aes128GcmRecordProtection(ObjectPool<SmartBuffer> bufferPool, ByteSpan masterSecret, ByteSpan serverRandom, ByteSpan clientRandom)
        {
            this.bufferPool = bufferPool;

            using SmartBuffer randomBuffer = this.bufferPool.GetObject();
            randomBuffer.Length = serverRandom.Length + clientRandom.Length;

            ByteSpan combinedRandom = (ByteSpan)randomBuffer;
            serverRandom.CopyTo(combinedRandom);
            clientRandom.CopyTo(combinedRandom.Slice(serverRandom.Length));

            // Expand master_secret to encryption keys
            const int ExpandedSize = 0
                + 0 // mac_key_length
                + 0 // mac_key_length
                + Aes128Gcm.KeySize // enc_key_length
                + Aes128Gcm.KeySize // enc_key_length
                + ImplicitNonceSize // fixed_iv_length
                + ImplicitNonceSize // fixed_iv_length
                ;

            ByteSpan expandedKey = new byte[ExpandedSize];
            PrfSha256.ExpandSecret(bufferPool, expandedKey, masterSecret, PrfLabel.KEY_EXPANSION, combinedRandom);

            ByteSpan clientWriteKey = expandedKey.Slice(0, Aes128Gcm.KeySize);
            ByteSpan serverWriteKey = expandedKey.Slice(Aes128Gcm.KeySize, Aes128Gcm.KeySize);
            this.clientWriteIV = expandedKey.Slice(2 * Aes128Gcm.KeySize, ImplicitNonceSize);
            this.serverWriteIV = expandedKey.Slice(2 * Aes128Gcm.KeySize + ImplicitNonceSize, ImplicitNonceSize);

            this.serverWriteCipher = new Aes128Gcm(serverWriteKey);
            this.clientWriteCipher = new Aes128Gcm(clientWriteKey);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.serverWriteCipher.Dispose();
            this.clientWriteCipher.Dispose();
        }

        /// <inheritdoc />
        private static int GetEncryptedSizeImpl(int dataSize)
        {
            return dataSize + Aes128Gcm.CiphertextOverhead;
        }

        /// <inheritdoc />
        public int GetEncryptedSize(int dataSize)
        {
            return GetEncryptedSizeImpl(dataSize);
        }

        private static int GetDecryptedSizeImpl(int dataSize)
        {
            return dataSize - Aes128Gcm.CiphertextOverhead;
        }

        /// <inheritdoc />
        public int GetDecryptedSize(int dataSize)
        {
            return GetDecryptedSizeImpl(dataSize);
        }

        /// <inheritdoc />
        public void EncryptServerPlaintext(ByteSpan output, ByteSpan input, ref Record record)
        {
            EncryptPlaintext(output, input, ref record, this.serverWriteCipher, this.serverWriteIV);
        }

        /// <inheritdoc />
        public void EncryptClientPlaintext(ByteSpan output, ByteSpan input, ref Record record)
        {
            EncryptPlaintext(output, input, ref record, this.clientWriteCipher, this.clientWriteIV);
        }

        private void EncryptPlaintext(ByteSpan output, ByteSpan input, ref Record record, Aes128Gcm cipher, ByteSpan writeIV)
        {
            Debug.Assert(output.Length >= GetEncryptedSizeImpl(input.Length));

            // Build GCM nonce (authenticated data)
            using SmartBuffer nonceBuffer = this.bufferPool.GetObject();
            nonceBuffer.Length = ImplicitNonceSize + ExplicitNonceSize;

            ByteSpan nonce = (ByteSpan)nonceBuffer;
            writeIV.CopyTo(nonce);
            nonce.WriteBigEndian16(record.Epoch, ImplicitNonceSize);
            nonce.WriteBigEndian48(record.SequenceNumber, ImplicitNonceSize + 2);

            // Serialize record as additional data
            Record plaintextRecord = record;
            plaintextRecord.Length = (ushort)input.Length;

            using SmartBuffer adataBuffer = this.bufferPool.GetObject();
            adataBuffer.Length = Record.Size;

            ByteSpan associatedData = (ByteSpan)adataBuffer;
            plaintextRecord.Encode(associatedData);

            cipher.Seal(output, nonce, input, associatedData);
        }

        /// <inheritdoc />
        public bool DecryptCiphertextFromServer(ByteSpan output, ByteSpan input, ref Record record)
        {
            return DecryptCiphertext(output, input, ref record, this.serverWriteCipher, this.serverWriteIV);
        }

        /// <inheritdoc />
        public bool DecryptCiphertextFromClient(ByteSpan output, ByteSpan input, ref Record record)
        {
            return DecryptCiphertext(output, input, ref record, this.clientWriteCipher, this.clientWriteIV);
        }

        private bool DecryptCiphertext(ByteSpan output, ByteSpan input, ref Record record, Aes128Gcm cipher, ByteSpan writeIV)
        {
            Debug.Assert(output.Length >= GetDecryptedSizeImpl(input.Length));

            // Build GCM nonce (authenticated data)
            using SmartBuffer nonceBuffer = this.bufferPool.GetObject();
            nonceBuffer.Length = ImplicitNonceSize + ExplicitNonceSize;

            ByteSpan nonce = (ByteSpan)nonceBuffer;
            writeIV.CopyTo(nonce);
            nonce.WriteBigEndian16(record.Epoch, ImplicitNonceSize);
            nonce.WriteBigEndian48(record.SequenceNumber, ImplicitNonceSize + 2);

            // Serialize record as additional data
            Record plaintextRecord = record;
            plaintextRecord.Length = (ushort)GetDecryptedSizeImpl(input.Length);

            using SmartBuffer adataBuffer = this.bufferPool.GetObject();
            adataBuffer.Length = Record.Size;

            ByteSpan associatedData = (ByteSpan)adataBuffer;
            plaintextRecord.Encode(associatedData);

            return cipher.Open(output, nonce, input, associatedData);
        }
    }
}
