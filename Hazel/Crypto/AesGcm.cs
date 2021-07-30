using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Hazel.Crypto
{
    /// <summary>
    /// Implementation of AEAD_AES128_GCM based on:
    ///  * RFC 5116 [1]
    ///  * NIST SP 800-38d [2]
    ///
    /// [1] https://tools.ietf.org/html/rfc5116
    /// [2] https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication800-38d.pdf
    ///
    /// Adapted from: https://gist.github.com/mendsley/777e6bd9ae7eddcb2b0c0fe18247dc60
    /// </summary>
    public class Aes128Gcm : IDisposable
    {
        public const int KeySize = 16;
        public const int NonceSize = 12;
        public const int CiphertextOverhead = TagSize;

        private const int TagSize = 16;

        private readonly IAes encryptor_;

        private readonly ByteSpan hashSubkey_;
        private readonly ByteSpan blockJ_;
        private readonly ByteSpan blockS_;
        private readonly ByteSpan blockZ_;
        private readonly ByteSpan blockV_;
        private readonly ByteSpan blockScratch_;

        /// <summary>
        /// Creates a new instance of an AEAD_AES128_GCM cipher
        /// </summary>
        /// <param name="key">Symmetric key</param>
        public Aes128Gcm(ByteSpan key)
        {
            if (key.Length != KeySize)
            {
                throw new ArgumentException("Invalid key length", nameof(key));
            }

            // Create the AES block cipher
            this.encryptor_ = CryptoProvider.CreateAes(key);

            // Allocate scratch space
            ByteSpan scratchSpace = new byte[96];
            this.hashSubkey_ = scratchSpace.Slice(0, 16);
            this.blockJ_ = scratchSpace.Slice(16, 16);
            this.blockS_ = scratchSpace.Slice(32, 16);
            this.blockZ_ = scratchSpace.Slice(48, 16);
            this.blockV_ = scratchSpace.Slice(64, 16);
            this.blockScratch_ = scratchSpace.Slice(80, 16);

            // Create the GHASH subkey by encrypting the 0-block
            this.encryptor_.EncryptBlock(this.hashSubkey_, this.hashSubkey_);
        }

        /// <summary>
        /// Encryptes the specified plaintext and generates an authentication
        /// tag for the provided additional data. Returns the byte array
        /// containg both the ciphertext and authentication tag.
        /// </summary>
        /// <param name="output">
        /// Array in which to encode the encrypted ciphertext and
        /// authentication tag. This array must be large enough to hold
        /// `plaintext.Lengh + CiphertextOverhead` bytes.
        /// </param>
        /// <param name="nonce">Unique value for this message</param>
        /// <param name="plaintext">Plaintext data to encrypt</param>
        /// <param name="associatedData">
        /// Additional data used to authenticate the message
        /// </param>
        public void Seal(ByteSpan output, ByteSpan nonce, ByteSpan plaintext, ByteSpan associatedData)
        {
            if (nonce.Length != NonceSize)
            {
                throw new ArgumentException("Invalid nonce size", nameof(nonce));
            }
            if (output.Length < plaintext.Length + CiphertextOverhead)
            {
                throw new ArgumentException("Invalid output size", nameof(output));
            }

            // Create the initial counter block
            nonce.CopyTo(this.blockJ_);

            // Encrypt the plaintext to output
            GCTR(output, this.blockJ_, 2, plaintext);

            // Generate and append the authentication tag
            int tagOffset = plaintext.Length;
            GenerateAuthenticationTag(output.Slice(tagOffset), output.Slice(0, tagOffset), associatedData);
        }

        /// <summary>
        /// Validates the authentication tag against the provided additional
        /// data, then decrypts the cipher text returning the original
        /// plaintext.
        /// </summary>
        /// <param name="nonce">
        /// The unique value used to seal this message
        /// </param>
        /// <param name="ciphertext">
        /// Combined ciphertext and authentication tag
        /// </param>
        /// <param name="associatedData">
        /// Additional data used to authenticate the message
        /// </param>
        /// <param name="output">
        /// On successful validation and decryprion, Open writes the original
        /// plaintext to output. Must contain enough space to hold
        /// `ciphertext.Length - CiphertextOverhead` bytes.
        /// </param>
        /// <returns>
        /// True if the data was validated and successfully decrypted.
        /// Otherwise, false.
        /// </returns>
        public bool Open(ByteSpan output, ByteSpan nonce, ByteSpan ciphertext, ByteSpan associatedData)
        {
            if (nonce.Length != NonceSize)
            {
                throw new ArgumentException("Invalid nonce size", nameof(nonce));
            }
            if (ciphertext.Length < CiphertextOverhead)
            {
                throw new ArgumentException("Invalid ciphertext size", nameof(ciphertext));
            }
            else if (output.Length < ciphertext.Length - CiphertextOverhead)
            {
                throw new ArgumentException("Invalid output size", nameof(output));
            }

            // Split ciphertext into actual ciphertext and authentication
            // tag components.
            ByteSpan authenticationTag = ciphertext.Slice(ciphertext.Length - TagSize);
            ciphertext = ciphertext.Slice(0, ciphertext.Length - TagSize);

            // Create the initial counter block
            nonce.CopyTo(this.blockJ_);

            // Verify the tags match
            GenerateAuthenticationTag(this.blockScratch_, ciphertext, associatedData);
            if (0 == Const.ConstantCompareSpans(this.blockScratch_, authenticationTag))
            {
                return false;
            }

            // Decrypt the cipher text to output
            GCTR(output, this.blockJ_, 2, ciphertext);
            return true;
        }

        /// <summary>
        /// Release resources acquired by the cipher
        /// </summary>
        public void Dispose()
        {
            this.encryptor_.Dispose();
        }

        // Generate the authentication tag for a ciphertext+associated data
        void GenerateAuthenticationTag(ByteSpan output, ByteSpan ciphertext, ByteSpan associatedData)
        {
            Debug.Assert(output.Length >= 16);

            // Hash `Associated data || Ciphertext || len(AssociatedD data) || len(Ciphertext)`
            // into `blockS`
            {
                // Clear hash output block
                SetSpanToZeros(this.blockS_);

                // Write associated data blocks to hash
                int fullBlocks = associatedData.Length / 16;
                GHASH(this.blockS_, associatedData, fullBlocks);
                if (fullBlocks * 16 < associatedData.Length)
                {
                    SetSpanToZeros(this.blockScratch_);
                    associatedData.Slice(fullBlocks * 16).CopyTo(this.blockScratch_);
                    GHASH(this.blockS_, this.blockScratch_, 1);
                }

                // Write ciphertext blocks to hash
                fullBlocks = ciphertext.Length / 16;
                GHASH(this.blockS_, ciphertext, fullBlocks);
                if (fullBlocks * 16 < ciphertext.Length)
                {
                    SetSpanToZeros(this.blockScratch_);
                    ciphertext.Slice(fullBlocks * 16).CopyTo(this.blockScratch_);
                    GHASH(this.blockS_, this.blockScratch_, 1);
                }

                // Write bit sizes to hash
                ulong associatedDataLengthInBits = (ulong)(8 * associatedData.Length);
                ulong ciphertextDataLengthInBits = (ulong)(8 * ciphertext.Length);
                this.blockScratch_.WriteBigEndian64(associatedDataLengthInBits);
                this.blockScratch_.WriteBigEndian64(ciphertextDataLengthInBits, 8);

                GHASH(this.blockS_, this.blockScratch_, 1);
            }

            // Encrypt the tag. GCM requires this because `GASH` is not
            // cryptographically secure. An attacker could derive our hash
            // subkey `hashSubkey_` from an unencrypted tag.
            GCTR(output, this.blockJ_, 1, this.blockS_);
        }

        // Run the GCTR cipher
        void GCTR(ByteSpan output, ByteSpan counterBlock, uint counter, ByteSpan data)
        {
            Debug.Assert(counterBlock.Length == 16);
            Debug.Assert(output.Length >= data.Length);

            // Loop through plaintext blocks
            int writeIndex = 0;
            int numBlocks = (data.Length + 15) / 16;
            for (int ii = 0; ii != numBlocks; ++ii)
            {
                // Encode counter into block
                // CB[1] = J0
                // CB[i] = inc[32](CB[i-1])
                counterBlock.WriteBigEndian32(counter, 12);
                ++counter;

                // CIPH[k](CB[i])
                this.encryptor_.EncryptBlock(counterBlock.Slice(0, 16), this.blockScratch_);

                // Y[i] = X[i] xor CIPH[k](CB[i])
                for (int jj = 0; jj != 16 && writeIndex < data.Length; ++jj, ++writeIndex)
                {
                    output[writeIndex] = (byte)(data[writeIndex] ^ this.blockScratch_[jj]);
                }
            }
        }

        // Run the GHASH function
        void GHASH(ByteSpan output, ByteSpan data, int numBlocks)
        {
            ///TODO(mendsley): See Ref[6] for opitmizations of GHASH on both hardware and software
            ///
            ///[6] D. McGrew, J. Viega, The Galois/Counter Mode of Operation (GCM), Natl. Inst. Stand.
            ///Technol. [Web page], http://www.csrc.nist.gov/groups/ST/toolkit/BCM/documents/
            ///proposedmodes / gcm / gcm - revised - spec.pdf, May 31, 2005.

            Debug.Assert(output.Length == 16);
            Debug.Assert(data.Length >= numBlocks * 16);

            int readIndex = 0;
            for (int ii = 0; ii != numBlocks; ++ii)
            {
                for (int jj = 0; jj != 16; ++jj, ++readIndex)
                {
                    // Y[ii-1] xor X[ii]
                    output[jj] ^= data[readIndex];
                }

                // Y[ii] = (Y[ii-1] xor X[ii]) · H
                MultiplyGF128Elements(output, this.hashSubkey_, this.blockZ_, this.blockV_);
            }
        }

        // Multiply two Galois field elements `X` and `Y` together and store
        // the result in `X` such that at the end of the function:
        //      X = X·Y
        static void MultiplyGF128Elements(ByteSpan X, ByteSpan Y, ByteSpan scratchZ, ByteSpan scratchV)
        {
            Debug.Assert(X.Length == 16);
            Debug.Assert(Y.Length == 16);
            Debug.Assert(scratchZ.Length == 16);
            Debug.Assert(scratchV.Length == 16);

            // Galois (finite) fields represented by GF(p) define a set of
            // closed algebraic operations. For AES128_GCM we'll be dealing
            // with the GF(2^128) field.
            //
            // We treat each incoming 16 byte block as a polynomial in field
            // and define multiplication between two polynomials as the
            // polynomial product reduced by (mod) the field polynomial:
            //      1 + x + x^2 + x^7 + x^128
            //
            // Field polynomials are represented by a 128 bit string. Bit n is
            // the coefficient of the x^n term. We use little-endian bit
            // ordering (not to be confused with byte ordering) for these
            // coefficients. E.g. X[0] & 0x00000001 represents the 7th bit in
            // the bit string defined by X, _not_ the 0th bit.
            //

            // What follows is a modified version of the "peasant's algorithm"
            // to multiply two numbers:
            //
            // Z contains the accumulated product
            // V is a copy of Y (so we can modify it via shifting).
            //
            // We calculate Z = X·V as follows
            //  We loop through each of the 128 bits in X maintaining the
            //  following loop invariant: X·V + Z = the final product
            //
            // On each iteration `ii`:
            //
            //   If the `ii`th bit of `X` is set, add the add the polynomial
            //   in `V` to `X`: `X[n] = X[n] ^ V[n]`
            //
            //   Double V (Shift one bit right since we're storing little
            //   endian bit). This has the effect of multiplying V by the
            //   polynomial `x`. We track the unrepresentable coefficient
            //   of `x^128` by storing the most significant bit before the
            //   shift `V[15] >> 7` as `carry`
            //
            //   Check if we've overflowed our multiplication. If overflow
            //   occurred, there will be a non-zero coefficient for the
            //   `x^128` term in the step above `carry`
            //
            //   If we have overflowed, our polynomial is exactly of degree
            //   129 (since we're only multiplying by `x`). We reduce the
            //   polynomial back into degree 128 by adding our field's
            //   irreducible polynomial: 1 + x + x^2 + x^7 + x^128. This
            //   reduction cancels out the x^128 term (x^128 + x^128 in GF(2)
            //   is zero). Therefore this modulo can be achieved by simply
            //   adding the irreducible polynomial to the new value of `V`. The
            //   irreducible polynomial is represented by the bit string:
            //   `11100001` followed by 120 `0`s. We can add this value to `V`
            //   by: `V[0] = V[0] ^ 0xE1`.
            SetSpanToZeros(scratchZ);
            X.CopyTo(scratchV);

            for (int ii = 0; ii != 128; ++ii)
            {
                int bitIndex = 7 - (ii % 8);
                if ((Y[ii / 8] & (1 << bitIndex)) != 0)
                {
                    for (int jj = 0; jj != 16; ++jj)
                    {
                        scratchZ[jj] ^= scratchV[jj];
                    }
                }

                bool carry = false;
                for (int jj = 0; jj != 16; ++jj)
                {
                    bool newCarry = (scratchV[jj] & 0x01) != 0;
                    scratchV[jj] >>= 1;
                    if (carry)
                    {
                        scratchV[jj] |= 0x80;
                    }
                    carry = newCarry;
                }

                if (carry)
                {
                    scratchV[0] ^= 0xE1;
                }
            }

            scratchZ.CopyTo(X);
        }

        // Set the contents of a span to all zero
        static void SetSpanToZeros(ByteSpan span)
        {
            for (int ii = 0, nn = span.Length; ii != nn; ++ii)
            {
                span[ii] = 0;
            }
        }
    }
}
