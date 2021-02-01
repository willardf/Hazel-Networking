using Hazel.Udp;
using System.Net;
using System.Security.Cryptography;

namespace Hazel.Dtls
{
    /// <summary>
    /// Connects to a UDP-DTLS server
    /// </summary>
    /// <inheritdoc />
    public abstract class DtlsUnityConnection : UnityUdpClientConnection
    {
        private RSA[] serverPublicKeys;

        /// <summary>
        /// Create a new instance of the DTLS connection
        /// </summary>
        /// <inheritdoc />
        public DtlsUnityConnection(IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4)
            : base(remoteEndPoint, ipMode)
        {
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <summary>
        /// Set the list of server public keys
        /// </summary>
        /// <param name="serverPublicKeys">
        /// List of public keys of authentic servers
        /// </param>
        public void SetPublicKeys(RSA[] serverPublicKeys)
        {
            if (this.serverPublicKeys != null)
            {
                foreach (RSA publicKey in this.serverPublicKeys)
                {
                    publicKey?.Dispose();
                }
            }

            this.serverPublicKeys = serverPublicKeys;
        }

        /// <summary>
        /// Abort the existing connection and restart the process
        /// </summary>
        protected override void RestartConnection()
        {
            throw new System.NotImplementedException();
            base.RestartConnection();
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnection(byte[] bytes, int length)
        {
            throw new System.NotImplementedException();
        }

        /// <inheritdoc />
        protected internal override void HandleReceive(MessageReader message, int bytesReceived)
        {
            throw new System.NotImplementedException();
        }
    }
}
