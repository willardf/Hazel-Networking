using Hazel.Dtls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;

namespace Hazel.UnitTests.Dtls
{
    [TestClass]
    public class X25519EcdheRsaSha256Tests
    {
        private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();
        private readonly RSA privateKey = RSA.Create();
        private readonly RSA publicKey;

        public X25519EcdheRsaSha256Tests()
        {
            RSAParameters keyParameters = this.privateKey.ExportParameters(false);
            this.publicKey = RSA.Create();
            this.publicKey.ImportParameters(keyParameters);
        }

        [TestMethod]
        public void SmallServerDataFails()
        {
            byte[] data;

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                int expectedSize = cipherSuite.CalculateServerMessageSize(this.privateKey);
                Assert.IsTrue(expectedSize/2 > 1);

                data = new byte[expectedSize/2];
                random.GetBytes(data);
            }

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                byte[] sharedKey = new byte[cipherSuite.SharedKeySize()];
                Assert.IsFalse(cipherSuite.VerifyServerMessageAndGenerateSharedKey(sharedKey, data, this.publicKey));
            }
        }

        [TestMethod]
        public void LargeServerDataFails()
        {
            byte[] data;

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                int expectedSize = cipherSuite.CalculateServerMessageSize(this.privateKey);
                Assert.IsTrue(expectedSize > 0);

                data = new byte[expectedSize * 2];
                random.GetBytes(data);
            }

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                byte[] sharedKey = new byte[cipherSuite.SharedKeySize()];
                Assert.IsFalse(cipherSuite.VerifyServerMessageAndGenerateSharedKey(sharedKey, data, this.publicKey));
            }
        }

        [TestMethod]
        public void RandomServerDataFails()
        {
            byte[] data;

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                int expectedSize = cipherSuite.CalculateServerMessageSize(this.privateKey);
                Assert.IsTrue(expectedSize > 0);

                data = new byte[expectedSize];
                random.GetBytes(data);
            }

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                byte[] sharedKey = new byte[cipherSuite.SharedKeySize()];
                Assert.IsFalse(cipherSuite.VerifyServerMessageAndGenerateSharedKey(sharedKey, data, this.publicKey));
            }
        }

        [TestMethod]
        public void SmallClientDataFails()
        {
            byte[] data;

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                int expectedSize = cipherSuite.CalculateClientMessageSize();
                Assert.IsTrue(expectedSize / 2 > 1);

                data = new byte[expectedSize / 2];
                random.GetBytes(data);
            }

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                byte[] sharedKey = new byte[cipherSuite.SharedKeySize()];
                Assert.IsFalse(cipherSuite.VerifyClientMessageAndGenerateSharedKey(sharedKey, data));
            }
        }

        [TestMethod]
        public void LargeClientDataFails()
        {
            byte[] data;

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                int expectedSize = cipherSuite.CalculateClientMessageSize();
                Assert.IsTrue(expectedSize > 0);

                data = new byte[expectedSize * 2];
                random.GetBytes(data);
            }

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                byte[] sharedKey = new byte[cipherSuite.SharedKeySize()];
                Assert.IsFalse(cipherSuite.VerifyClientMessageAndGenerateSharedKey(sharedKey, data));
            }
        }

        [TestMethod]
        public void RandomClientDataFails()
        {
            byte[] data;

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                int expectedSize = cipherSuite.CalculateClientMessageSize();
                Assert.IsTrue(expectedSize > 0);

                data = new byte[expectedSize];
                random.GetBytes(data);
            }

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                byte[] sharedKey = new byte[cipherSuite.SharedKeySize()];
                Assert.IsFalse(cipherSuite.VerifyClientMessageAndGenerateSharedKey(sharedKey, data));
            }
        }

        [TestMethod]
        public void RandomSignatureFails()
        {
            byte[] data;

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                int expectedSize = cipherSuite.CalculateServerMessageSize(this.privateKey);
                Assert.IsTrue(expectedSize > 0);

                data = new byte[expectedSize];
                cipherSuite.EncodeServerKeyExchangeMessage(data, this.privateKey);
            }

            // overwrite signature with random data
            byte[] randomSignature = new byte[this.privateKey.KeySize/8];
            random.GetBytes(randomSignature);
            new ByteSpan(randomSignature).CopyTo(new ByteSpan(data, data.Length - randomSignature.Length, randomSignature.Length));

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                byte[] sharedKey = new byte[cipherSuite.SharedKeySize()];
                Assert.IsFalse(cipherSuite.VerifyServerMessageAndGenerateSharedKey(sharedKey, data, this.publicKey));
            }
        }

        [TestMethod]
        public void VerifySignature()
        {
            byte[] data;

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                int expectedSize = cipherSuite.CalculateServerMessageSize(this.privateKey);
                Assert.IsTrue(expectedSize > 0);

                data = new byte[expectedSize];
                cipherSuite.EncodeServerKeyExchangeMessage(data, this.privateKey);
            }

            using (X25519EcdheRsaSha256 cipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                byte[] sharedKey = new byte[cipherSuite.SharedKeySize()];
                Assert.IsTrue(cipherSuite.VerifyServerMessageAndGenerateSharedKey(sharedKey, data, this.publicKey));
            }
        }

        [TestMethod]
        public void GeneratesSameSharedKey()
        {
            byte[] serverSharedSecret;
            byte[] clientSharedSecret;

            using (X25519EcdheRsaSha256 serverCipherSuite = new X25519EcdheRsaSha256(this.random))
            {
                int expectedSize = serverCipherSuite.CalculateServerMessageSize(this.privateKey);
                Assert.IsTrue(expectedSize > 0);

                byte[] serverKeyExchangeMessage = new byte[expectedSize];
                serverCipherSuite.EncodeServerKeyExchangeMessage(serverKeyExchangeMessage, this.privateKey);

                byte[] clientKeyExchange;

                using (X25519EcdheRsaSha256 clientCipherSuite = new X25519EcdheRsaSha256(this.random))
                {
                    clientSharedSecret = new byte[clientCipherSuite.SharedKeySize()];
                    Assert.IsTrue(clientCipherSuite.VerifyServerMessageAndGenerateSharedKey(clientSharedSecret, serverKeyExchangeMessage, this.publicKey));

                    clientKeyExchange = new byte[clientCipherSuite.CalculateClientMessageSize()];
                    clientCipherSuite.EncodeClientKeyExchangeMessage(clientKeyExchange);
                }

                serverSharedSecret = new byte[serverCipherSuite.SharedKeySize()];
                Assert.IsTrue(serverCipherSuite.VerifyClientMessageAndGenerateSharedKey(serverSharedSecret, clientKeyExchange));
            }

            CollectionAssert.AreEqual(serverSharedSecret, clientSharedSecret);
        }
    }
}
