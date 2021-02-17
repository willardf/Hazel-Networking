using Hazel.Crypto;
using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Hazel.Dtls
{
    /// <summary>
    /// ECDHE_RSA_*_256 cipher suite
    /// </summary>
    public class X25519EcdheRsaSha256 : IHandshakeCipherSuite
    {
        private readonly ByteSpan privateAgreementKey;
        private SHA256 sha256 = SHA256.Create();

        /// <summary>
        /// Create a new instance of the x25519 key exchange
        /// </summary>
        /// <param name="random">Random data source</param>
        public X25519EcdheRsaSha256(RandomNumberGenerator random)
        {
            byte[] buffer = new byte[X25519.KeySize];
            random.GetBytes(buffer);
            this.privateAgreementKey = buffer;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.sha256?.Dispose();
            this.sha256 = null;
        }

        /// <inheritdoc />
        public int SharedKeySize()
        {
            return X25519.KeySize;
        }

        /// <summary>
        /// Calculate the server message size given an RSA key size
        /// </summary>
        /// <param name="keySize">
        /// Size of the private key (in bits)
        /// </param>
        /// <returns>
        /// Size of the ServerKeyExchange message in bytes
        /// </returns>
        private static int CalculateServerMessageSize(int keySize)
        {
            int signatureSize = keySize / 8;

            return 0
                + 1 // ECCurveType ServerKeyExchange.params.curve_params.curve_type
                + 2 // NamedCurve ServerKeyExchange.params.curve_params.namedcurve
                + 1 + X25519.KeySize // ECPoint ServerKeyExchange.params.public
                + 1 // HashAlgorithm ServerKeyExchange.algorithm.hash
                + 1 // SignatureAlgorithm ServerKeyExchange.signed_params.algorithm.signature
                + 2 // ServerKeyExchange.signed_params.size
                + signatureSize // ServerKeyExchange.signed_params.opaque
                ;
        }

        /// <inheritdoc />
        public int CalculateServerMessageSize(object privateKey)
        {
            RSA rsaPrivateKey = privateKey as RSA;
            if (rsaPrivateKey == null)
            {
                throw new ArgumentException("Invalid private key", nameof(privateKey));
            }

            return CalculateServerMessageSize(rsaPrivateKey.KeySize);
        }

        /// <inheritdoc />
        public void EncodeServerKeyExchangeMessage(ByteSpan output, object privateKey)
        {
            RSA rsaPrivateKey = privateKey as RSA;
            if (rsaPrivateKey == null)
            {
                throw new ArgumentException("Invalid private key", nameof(privateKey));
            }

            output[0] = (byte)ECCurveType.NamedCurve;
            output.WriteBigEndian16((ushort)NamedCurve.x25519, 1);
            output[3] = (byte)X25519.KeySize;
            X25519.Func(output.Slice(4, X25519.KeySize), this.privateAgreementKey);

            // Hash the key parameters
            byte[] paramterDigest = this.sha256.ComputeHash(output.GetUnderlyingArray(), output.Offset, 4 + X25519.KeySize);

            // Sign the paramter digest
            RSAPKCS1SignatureFormatter signer = new RSAPKCS1SignatureFormatter(rsaPrivateKey);
            signer.SetHashAlgorithm("SHA256");
            ByteSpan signature = signer.CreateSignature(paramterDigest);

            Debug.Assert(signature.Length == rsaPrivateKey.KeySize/8);
            output[4 + X25519.KeySize] = (byte)HashAlgorithm.Sha256;
            output[5 + X25519.KeySize] = (byte)SignatureAlgorithm.RSA;
            output.Slice(6+X25519.KeySize).WriteBigEndian16((ushort)signature.Length);
            signature.CopyTo(output.Slice(8+X25519.KeySize));
        }

        /// <inheritdoc />
        public bool VerifyServerMessageAndGenerateSharedKey(ByteSpan output, ByteSpan serverKeyExchangeMessage, object publicKey)
        {
            RSA rsaPublicKey = publicKey as RSA;
            if (rsaPublicKey == null)
            {
                return false;
            }
            else if (output.Length != X25519.KeySize)
            {
                return false;
            }

            // Verify message is compatible with this cipher suite
            if (serverKeyExchangeMessage.Length != CalculateServerMessageSize(rsaPublicKey.KeySize))
            {
                return false;
            }
            else if (serverKeyExchangeMessage[0] != (byte)ECCurveType.NamedCurve)
            {
                return false;
            }
            else if (serverKeyExchangeMessage.ReadBigEndian16(1) != (ushort)NamedCurve.x25519)
            {
                return false;
            }
            else if (serverKeyExchangeMessage[3] != X25519.KeySize)
            {
                return false;
            }
            else if (serverKeyExchangeMessage[4 + X25519.KeySize] != (byte)HashAlgorithm.Sha256)
            {
                return false;
            }
            else if (serverKeyExchangeMessage[5 + X25519.KeySize] != (byte)SignatureAlgorithm.RSA)
            {
                return false;
            }

            ByteSpan keyParameters = serverKeyExchangeMessage.Slice(0, 4+X25519.KeySize);
            ByteSpan othersPublicKey = keyParameters.Slice(4);
            ushort signatureSize = serverKeyExchangeMessage.ReadBigEndian16(6 + X25519.KeySize);
            ByteSpan signature = serverKeyExchangeMessage.Slice(4+keyParameters.Length);

            if (signatureSize != signature.Length)
            {
                return false;
            }

            // Hash the key parameters
            byte[] parameterDigest = this.sha256.ComputeHash(keyParameters.GetUnderlyingArray(), keyParameters.Offset, keyParameters.Length);

            // Verify the signature
            RSAPKCS1SignatureDeformatter verifier = new RSAPKCS1SignatureDeformatter(rsaPublicKey);
            verifier.SetHashAlgorithm("SHA256");
            if (!verifier.VerifySignature(parameterDigest, signature.ToArray()))
            {
                return false;
            }

            // Signature has been validated, generate the shared key
            return X25519.Func(output, this.privateAgreementKey, othersPublicKey);
        }

        private static int ClientMessageSize = 0
                + 1 + X25519.KeySize // ECPoint ClientKeyExchange.ecdh_Yc
                ;

        /// <inheritdoc />
        public int CalculateClientMessageSize()
        {
            return ClientMessageSize;
        }

        /// <inheritdoc />
        public void EncodeClientKeyExchangeMessage(ByteSpan output)
        {
            output[0] = (byte)X25519.KeySize;
            X25519.Func(output.Slice(1, X25519.KeySize), this.privateAgreementKey);
        }

        /// <inheritdoc />
        public bool VerifyClientMessageAndGenerateSharedKey(ByteSpan output, ByteSpan clientKeyExchangeMessage)
        {
            if (clientKeyExchangeMessage.Length != ClientMessageSize)
            {
                return false;
            }
            else if (clientKeyExchangeMessage[0] != (byte)X25519.KeySize)
            {
                return false;
            }

            ByteSpan othersPublicKey = clientKeyExchangeMessage.Slice(1);
            return X25519.Func(output, this.privateAgreementKey, othersPublicKey);
        }
    }
}
