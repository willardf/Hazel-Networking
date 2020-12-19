using System;

namespace Hazel.Dtls
{
    /// <summary>
    /// DTLS cipher suite interface for the handshake portion of
    /// the connection.
    /// </summary>
    public interface IHandshakeCipherSuite : IDisposable
    {
        /// <summary>
        /// Gets the size of the shared key
        /// </summary>
        /// <returns>Size of the shared key in bytes </returns>
        int SharedKeySize();

        /// <summary>
        /// Calculate the size of the ServerKeyExchnage message
        /// </summary>
        /// <param name="privateKey">
        /// Private key that will be used to sign the message
        /// </param>
        /// <returns>Size of the message in bytes</returns>
        int CalculateServerMessageSize(object privateKey);

        /// <summary>
        /// Encodes the ServerKeyExchange message
        /// </summary>
        /// <param name="privateKey">Private key to use for signing</param>
        void EncodeServerKeyExchangeMessage(ByteSpan output, object privateKey);

        /// <summary>
        /// Verifies the authenticity of a server key exchange
        /// message and calculates the shared secret.
        /// </summary>
        /// <returns>
        /// True if the authenticity has been validated and a shared key
        /// was generated. Otherwise, false.
        /// </returns>
        bool VerifyServerMessageAndGenerateSharedKey(ByteSpan output, ByteSpan serverKeyExchangeMessage, object publicKey);

        /// <summary>
        /// Calculate the size of the ClientKeyExchange message
        /// </summary>
        /// <returns>Size of the message in bytes</returns>
        int CalculateClientMessageSize();

        /// <summary>
        /// Encodes the ClientKeyExchangeMessage
        /// </summary>
        void EncodeClientKeyExchangeMessage(ByteSpan output);

        /// <summary>
        /// Verifies the validity of a client key exchange message
        /// and calculats the hsared secret.
        /// </summary>
        /// <returns>
        /// True if the client exchange message is valid and a
        /// shared key was generated. Otherwise, false.
        /// </returns>
        bool VerifyClientMessageAndGenerateSharedKey(ByteSpan output, ByteSpan clientKeyExchangeMessage);
    }
}
