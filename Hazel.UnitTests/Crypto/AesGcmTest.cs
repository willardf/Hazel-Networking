using Hazel.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace Hazel.UnitTests.Crypto
{
    [TestClass]
    public class AesGcmTest
    {
        [TestMethod]
        public void Example1()
        {
            byte[] key = Utils.HexToBytes("FEFFE992 8665731C 6D6A8F94 67308308");
            byte[] nonce = Utils.HexToBytes("CAFEBABE FACEDBAD DECAF888");
            byte[] associatedData = Utils.HexToBytes("");

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] plaintext = Utils.HexToBytes("");
                byte[] ciphertextBytes = new byte[plaintext.Length + Aes128Gcm.CiphertextOverhead];
                aes.Seal(ciphertextBytes, nonce, plaintext, associatedData);

                CollectionAssert.AreEqual(Utils.HexToBytes("3247184B 3C4F69A4 4DBCD228 87BBB418"), ciphertextBytes);
            }

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] ciphertext = Utils.HexToBytes("3247184B 3C4F69A4 4DBCD228 87BBB418");
                byte[] plaintext = new byte[ciphertext.Length - Aes128Gcm.CiphertextOverhead];
                bool result = aes.Open(plaintext, nonce, ciphertext, associatedData);
                Assert.IsTrue(result);
                CollectionAssert.AreEqual(Utils.HexToBytes(""), plaintext);
            }
        }

        [TestMethod]
        public void Example2()
        {
            byte[] key = Utils.HexToBytes("FEFFE992 8665731C 6D6A8F94 67308308");
            byte[] nonce = Utils.HexToBytes("CAFEBABE FACEDBAD DECAF888");
            byte[] associatedData = Utils.HexToBytes("");

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] plaintext = Utils.HexToBytes(@"
                    D9313225 F88406E5 A55909C5 AFF5269A
                    86A7A953 1534F7DA 2E4C303D 8A318A72
                    1C3C0C95 95680953 2FCF0E24 49A6B525
                    B16AEDF5 AA0DE657 BA637B39 1AAFD255
                ");
                byte[] ciphertextBytes = new byte[plaintext.Length + Aes128Gcm.CiphertextOverhead];
                aes.Seal(ciphertextBytes, nonce, plaintext, associatedData);

                CollectionAssert.AreEqual(Utils.HexToBytes(@"
                    42831EC2 21777424 4B7221B7 84D0D49C
                    E3AA212F 2C02A4E0 35C17E23 29ACA12E
                    21D514B2 5466931C 7D8F6A5A AC84AA05
                    1BA30B39 6A0AAC97 3D58E091 473F5985

                    4D5C2AF3 27CD64A6 2CF35ABD 2BA6FAB4
                "), ciphertextBytes);
            }

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] ciphertext = Utils.HexToBytes(@"
                    42831EC2 21777424 4B7221B7 84D0D49C
                    E3AA212F 2C02A4E0 35C17E23 29ACA12E
                    21D514B2 5466931C 7D8F6A5A AC84AA05
                    1BA30B39 6A0AAC97 3D58E091 473F5985

                    4D5C2AF3 27CD64A6 2CF35ABD 2BA6FAB4
                ");
                byte[] plaintext = new byte[ciphertext.Length - Aes128Gcm.CiphertextOverhead];
                bool result = aes.Open(plaintext, nonce, ciphertext, associatedData);
                Assert.IsTrue(result);

                CollectionAssert.AreEqual(Utils.HexToBytes(@"
                    D9313225 F88406E5 A55909C5 AFF5269A
                    86A7A953 1534F7DA 2E4C303D 8A318A72
                    1C3C0C95 95680953 2FCF0E24 49A6B525
                    B16AEDF5 AA0DE657 BA637B39 1AAFD255
                "), plaintext);
            }
        }

        [TestMethod]
        public void Example3()
        {
            byte[] key = Utils.HexToBytes("FEFFE992 8665731C 6D6A8F94 67308308");
            byte[] nonce = Utils.HexToBytes("CAFEBABE FACEDBAD DECAF888");
            byte[] associatedData = Utils.HexToBytes(@"
                3AD77BB4 0D7A3660 A89ECAF3 2466EF97
                F5D3D585 03B9699D E785895A 96FDBAAF
                43B1CD7F 598ECE23 881B00E3 ED030688
                7B0C785E 27E8AD3F 82232071 04725DD4
            ");

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] plaintext = Utils.HexToBytes("");
                byte[] ciphertextBytes = new byte[plaintext.Length + Aes128Gcm.CiphertextOverhead];
                aes.Seal(ciphertextBytes, nonce, plaintext, associatedData);

                CollectionAssert.AreEqual(Utils.HexToBytes(@"
                        5F91D771 23EF5EB9 99791384 9B8DC1E9
                    "), ciphertextBytes);
            }

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] ciphertext = Utils.HexToBytes(@"
                        5F91D771 23EF5EB9 99791384 9B8DC1E9
                    ");
                byte[] plaintext = new byte[ciphertext.Length - Aes128Gcm.CiphertextOverhead];
                bool result = aes.Open(plaintext, nonce, ciphertext, associatedData);
                Assert.IsTrue(result);

                CollectionAssert.AreEqual(Utils.HexToBytes(""), plaintext);
            }
        }

        [TestMethod]
        public void Example4()
        {
            byte[] key = Utils.HexToBytes("FEFFE992 8665731C 6D6A8F94 67308308");
            byte[] nonce = Utils.HexToBytes("CAFEBABE FACEDBAD DECAF888");
            byte[] associatedData = Utils.HexToBytes(@"
                3AD77BB4 0D7A3660 A89ECAF3 2466EF97
                F5D3D585 03B9699D E785895A 96FDBAAF
                43B1CD7F 598ECE23 881B00E3 ED030688
                7B0C785E 27E8AD3F 82232071 04725DD4
            ");

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] plaintext = Utils.HexToBytes(@"
                    D9313225 F88406E5 A55909C5 AFF5269A
                    86A7A953 1534F7DA 2E4C303D 8A318A72
                    1C3C0C95 95680953 2FCF0E24 49A6B525
                    B16AEDF5 AA0DE657 BA637B39 1AAFD255
                ");
                byte[] ciphertextBytes = new byte[plaintext.Length + Aes128Gcm.CiphertextOverhead];
                aes.Seal(ciphertextBytes, nonce, plaintext, associatedData);

                CollectionAssert.AreEqual(Utils.HexToBytes(@"
                    42831EC2 21777424 4B7221B7 84D0D49C
                    E3AA212F 2C02A4E0 35C17E23 29ACA12E
                    21D514B2 5466931C 7D8F6A5A AC84AA05
                    1BA30B39 6A0AAC97 3D58E091 473F5985

                    64C02329 04AF398A 5B67C10B 53A5024D
                "), ciphertextBytes);
            }

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] ciphertext = Utils.HexToBytes(@"
                    42831EC2 21777424 4B7221B7 84D0D49C
                    E3AA212F 2C02A4E0 35C17E23 29ACA12E
                    21D514B2 5466931C 7D8F6A5A AC84AA05
                    1BA30B39 6A0AAC97 3D58E091 473F5985

                    64C02329 04AF398A 5B67C10B 53A5024D
                ");
                byte[] plaintext = new byte[ciphertext.Length - Aes128Gcm.CiphertextOverhead];
                bool result = aes.Open(plaintext, nonce, ciphertext, associatedData);
                Assert.IsTrue(result);

                CollectionAssert.AreEqual(Utils.HexToBytes(@"
                    D9313225 F88406E5 A55909C5 AFF5269A
                    86A7A953 1534F7DA 2E4C303D 8A318A72
                    1C3C0C95 95680953 2FCF0E24 49A6B525
                    B16AEDF5 AA0DE657 BA637B39 1AAFD255
                "), plaintext);
            }
        }

        [TestMethod]
        public void TestReuseToDecrypt()
        {
            byte[] key = Utils.HexToBytes("FEFFE992 8665731C 6D6A8F94 67308308");
            byte[] nonce = Utils.HexToBytes("CAFEBABE FACEDBAD DECAF888");
            byte[] associatedData = Utils.HexToBytes(@"
                3AD77BB4 0D7A3660 A89ECAF3 2466EF97
                F5D3D585 03B9699D E785895A 96FDBAAF
                43B1CD7F 598ECE23 881B00E3 ED030688
                7B0C785E 27E8AD3F 82232071 04725DD4
            ");

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] plaintext = Utils.HexToBytes("");
                byte[] ciphertextBytes = new byte[plaintext.Length + Aes128Gcm.CiphertextOverhead];
                aes.Seal(ciphertextBytes, nonce, plaintext, associatedData);

                CollectionAssert.AreEqual(Utils.HexToBytes(@"
                        5F91D771 23EF5EB9 99791384 9B8DC1E9
                    "), ciphertextBytes);

                byte[] ciphertext = Utils.HexToBytes(@"
                        5F91D771 23EF5EB9 99791384 9B8DC1E9
                    ");
                plaintext = new byte[ciphertext.Length - Aes128Gcm.CiphertextOverhead];
                bool result = aes.Open(plaintext, nonce, ciphertext, associatedData);
                Assert.IsTrue(result);

                CollectionAssert.AreEqual(Utils.HexToBytes(""), plaintext);
            }
        }

        [TestMethod]
        public void TestPlaintextSmallerThanBlock()
        {
            byte[] key = Utils.HexToBytes("FEFFE992 8665731C 6D6A8F94 67308308");
            byte[] nonce = Utils.HexToBytes("CAFEBABE FACEDBAD DECAF888");
            byte[] originalPlaintext = Encoding.UTF8.GetBytes("Lorem ipsum");
            Assert.IsTrue(originalPlaintext.Length < 16);

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] ciphertext = new byte[originalPlaintext.Length + Aes128Gcm.CiphertextOverhead];
                aes.Seal(ciphertext, nonce, originalPlaintext, ByteSpan.Empty);

                byte[] plaintext = new byte[originalPlaintext.Length];
                bool result = aes.Open(plaintext, nonce, ciphertext, ByteSpan.Empty);
                Assert.IsTrue(result);

                CollectionAssert.AreEqual(originalPlaintext, plaintext);
            }
        }

        [TestMethod]
        public void TestPlaintextLargerThanBlockMultiple()
        {
            byte[] key = Utils.HexToBytes("FEFFE992 8665731C 6D6A8F94 67308308");
            byte[] nonce = Utils.HexToBytes("CAFEBABE FACEDBAD DECAF888");
            byte[] originalPlaintext = Encoding.UTF8.GetBytes("Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.");
            Assert.IsTrue(originalPlaintext.Length > 16);
            Assert.IsTrue((originalPlaintext.Length % 16) != 0);

            using (Aes128Gcm aes = new Aes128Gcm(key))
            {
                byte[] ciphertext = new byte[originalPlaintext.Length + Aes128Gcm.CiphertextOverhead];
                aes.Seal(ciphertext, nonce, originalPlaintext, ByteSpan.Empty);

                byte[] plaintext = new byte[originalPlaintext.Length];
                bool result = aes.Open(plaintext, nonce, ciphertext, ByteSpan.Empty);
                Assert.IsTrue(result);

                CollectionAssert.AreEqual(originalPlaintext, plaintext);
            }
        }
    }
}
