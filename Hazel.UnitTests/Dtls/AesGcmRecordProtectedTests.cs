using Hazel.Dtls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;
using System.Text;

namespace Hazel.UnitTests.Dtls
{
    [TestClass]
    public class AesGcmRecordProtectedTests
    {
        private readonly ByteSpan masterSecret;
        private readonly ByteSpan serverRandom;
        private readonly ByteSpan clientRandom;

        private const string TestMessage = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";

        public AesGcmRecordProtectedTests()
        {
            this.masterSecret = new byte[48];
            this.serverRandom = new byte[32];
            this.clientRandom = new byte[32];

            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(this.masterSecret.GetUnderlyingArray());
                random.GetBytes(this.serverRandom.GetUnderlyingArray());
                random.GetBytes(this.clientRandom.GetUnderlyingArray());
            }
        }

        [TestMethod]
        public void ServerCanEncryptAndDecryptData()
        {
            using (Aes128GcmRecordProtection recordProtection = new Aes128GcmRecordProtection(this.masterSecret, this.serverRandom, this.clientRandom))
            {
                byte[] messageAsBytes = Encoding.UTF8.GetBytes(TestMessage);

                Record record = new Record();
                record.ContentType = ContentType.ApplicationData;
                record.Epoch = 1;
                record.SequenceNumber = 124;
                record.Length = (ushort)recordProtection.GetEncryptedSize(messageAsBytes.Length);

                ByteSpan encrypted = new byte[record.Length];
                recordProtection.EncryptServerPlaintext(encrypted, messageAsBytes, ref record);

                ByteSpan plaintext = new byte[recordProtection.GetDecryptedSize(encrypted.Length)];
                bool couldDecrypt = recordProtection.DecryptCiphertextFromServer(plaintext, encrypted, ref record);
                Assert.IsTrue(couldDecrypt);
                Assert.AreEqual(messageAsBytes.Length, plaintext.Length);
                Assert.AreEqual(TestMessage, Encoding.UTF8.GetString(plaintext.GetUnderlyingArray(), plaintext.Offset, plaintext.Length));
            }
        }

        [TestMethod]
        public void ClientCanEncryptAndDecryptData()
        {
            using (Aes128GcmRecordProtection recordProtection = new Aes128GcmRecordProtection(this.masterSecret, this.serverRandom, this.clientRandom))
            {
                byte[] messageAsBytes = Encoding.UTF8.GetBytes(TestMessage);

                Record record = new Record();
                record.ContentType = ContentType.ApplicationData;
                record.Epoch = 1;
                record.SequenceNumber = 124;
                record.Length = (ushort)recordProtection.GetEncryptedSize(messageAsBytes.Length);

                ByteSpan encrypted = new byte[record.Length];
                recordProtection.EncryptClientPlaintext(encrypted, messageAsBytes, ref record);

                ByteSpan plaintext = new byte[recordProtection.GetDecryptedSize(encrypted.Length)];
                bool couldDecrypt = recordProtection.DecryptCiphertextFromClient(plaintext, encrypted, ref record);
                Assert.IsTrue(couldDecrypt);
                Assert.AreEqual(messageAsBytes.Length, plaintext.Length);
                Assert.AreEqual(TestMessage, Encoding.UTF8.GetString(plaintext.GetUnderlyingArray(), plaintext.Offset, plaintext.Length));
            }
        }

        [TestMethod]
        public void ServerDecryptionFailsWhenRecordModified()
        {
            using (Aes128GcmRecordProtection recordProtection = new Aes128GcmRecordProtection(this.masterSecret, this.serverRandom, this.clientRandom))
            {
                byte[] messageAsBytes = Encoding.UTF8.GetBytes(TestMessage);

                Record originalRecord = new Record();
                originalRecord.ContentType = ContentType.ApplicationData;
                originalRecord.Epoch = 1;
                originalRecord.SequenceNumber = 124;
                originalRecord.Length = (ushort)recordProtection.GetEncryptedSize(messageAsBytes.Length);

                ByteSpan encrypted = new byte[originalRecord.Length];
                recordProtection.EncryptServerPlaintext(encrypted, messageAsBytes, ref originalRecord);

                ByteSpan plaintext = new byte[recordProtection.GetDecryptedSize(encrypted.Length)];

                Record record = originalRecord;
                record.ContentType = ContentType.Handshake;
                bool couldDecrypt = recordProtection.DecryptCiphertextFromServer(plaintext, encrypted, ref record);
                Assert.IsFalse(couldDecrypt);

                record = originalRecord;
                record.Epoch++;
                couldDecrypt = recordProtection.DecryptCiphertextFromServer(plaintext, encrypted, ref record);
                Assert.IsFalse(couldDecrypt);

                record = originalRecord;
                record.SequenceNumber++;
                couldDecrypt = recordProtection.DecryptCiphertextFromServer(plaintext, encrypted, ref record);
                Assert.IsFalse(couldDecrypt);
            }
        }

        [TestMethod]
        public void ClientDecryptionFailsWhenRecordModified()
        {
            using (Aes128GcmRecordProtection recordProtection = new Aes128GcmRecordProtection(this.masterSecret, this.serverRandom, this.clientRandom))
            {
                byte[] messageAsBytes = Encoding.UTF8.GetBytes(TestMessage);

                Record originalRecord = new Record();
                originalRecord.ContentType = ContentType.ApplicationData;
                originalRecord.Epoch = 1;
                originalRecord.SequenceNumber = 124;
                originalRecord.Length = (ushort)recordProtection.GetEncryptedSize(messageAsBytes.Length);

                ByteSpan encrypted = new byte[originalRecord.Length];
                recordProtection.EncryptClientPlaintext(encrypted, messageAsBytes, ref originalRecord);

                ByteSpan plaintext = new byte[recordProtection.GetDecryptedSize(encrypted.Length)];

                Record record = originalRecord;
                record.ContentType = ContentType.Handshake;
                bool couldDecrypt = recordProtection.DecryptCiphertextFromClient(plaintext, encrypted, ref record);
                Assert.IsFalse(couldDecrypt);

                record = originalRecord;
                record.Epoch++;
                couldDecrypt = recordProtection.DecryptCiphertextFromClient(plaintext, encrypted, ref record);
                Assert.IsFalse(couldDecrypt);

                record = originalRecord;
                record.SequenceNumber++;
                couldDecrypt = recordProtection.DecryptCiphertextFromClient(plaintext, encrypted, ref record);
                Assert.IsFalse(couldDecrypt);
            }
        }

        [TestMethod]
        public void ServerEncryptionCanoverlap()
        {
            using (Aes128GcmRecordProtection recordProtection = new Aes128GcmRecordProtection(this.masterSecret, this.serverRandom, this.clientRandom))
            {
                ByteSpan messageAsBytes = Encoding.UTF8.GetBytes(TestMessage);

                Record record = new Record();
                record.ContentType = ContentType.ApplicationData;
                record.Epoch = 1;
                record.SequenceNumber = 124;
                record.Length = (ushort)recordProtection.GetEncryptedSize(messageAsBytes.Length);

                ByteSpan encrypted = new byte[record.Length];
                messageAsBytes.CopyTo(encrypted);
                recordProtection.EncryptServerPlaintext(encrypted, encrypted.Slice(0, messageAsBytes.Length), ref record);

                ByteSpan plaintext = encrypted.Slice(0, recordProtection.GetDecryptedSize(record.Length));
                bool couldDecrypt = recordProtection.DecryptCiphertextFromServer(plaintext, encrypted, ref record);
                Assert.IsTrue(couldDecrypt);
                Assert.AreEqual(messageAsBytes.Length, plaintext.Length);
                Assert.AreEqual(TestMessage, Encoding.UTF8.GetString(plaintext.GetUnderlyingArray(), plaintext.Offset, plaintext.Length));
            }
        }

        [TestMethod]
        public void ClientEncryptionCanoverlap()
        {
            using (Aes128GcmRecordProtection recordProtection = new Aes128GcmRecordProtection(this.masterSecret, this.serverRandom, this.clientRandom))
            {
                ByteSpan messageAsBytes = Encoding.UTF8.GetBytes(TestMessage);

                Record record = new Record();
                record.ContentType = ContentType.ApplicationData;
                record.Epoch = 1;
                record.SequenceNumber = 124;
                record.Length = (ushort)recordProtection.GetEncryptedSize(messageAsBytes.Length);

                ByteSpan encrypted = new byte[record.Length];
                messageAsBytes.CopyTo(encrypted);
                recordProtection.EncryptClientPlaintext(encrypted, encrypted.Slice(0, messageAsBytes.Length), ref record);

                ByteSpan plaintext = encrypted.Slice(0, recordProtection.GetDecryptedSize(record.Length));
                bool couldDecrypt = recordProtection.DecryptCiphertextFromClient(plaintext, encrypted, ref record);
                Assert.IsTrue(couldDecrypt);
                Assert.AreEqual(messageAsBytes.Length, plaintext.Length);
                Assert.AreEqual(TestMessage, Encoding.UTF8.GetString(plaintext.GetUnderlyingArray(), plaintext.Offset, plaintext.Length));
            }
        }
    }
}
