using Hazel.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace Hazel.UnitTests.Crypto
{
    [TestClass]
    public class Sha256Tests
    {
        [TestMethod]
        public void TestOneBlockMessage()
        {
            ByteSpan message = Encoding.ASCII.GetBytes(
                "abc"
            );
            byte[] expectedDigest = Utils.HexToBytes(
                "ba7816bf 8f01cfea 414140de 5dae2223 b00361a3 96177a9c b410ff61 f20015ad"
            );
            byte[] actualDigest = new byte[Sha256Stream.DigestSize];

            using (Sha256Stream sha256 = new Sha256Stream())
            {
                sha256.AddData(message);
                sha256.CalculateHash(actualDigest);
            }

            CollectionAssert.AreEqual(expectedDigest, actualDigest);
        }

        [TestMethod]
        public void TestMultiBlockMessage()
        {
            ByteSpan message = Encoding.ASCII.GetBytes(
                "abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"
            );
            byte[] expectedDigest = Utils.HexToBytes(
                "248d6a61 d20638b8 e5c02693 0c3e6039 a33ce459 64ff2167 f6ecedd4 19db06c1"
            );
            byte[] actualDigest = new byte[Sha256Stream.DigestSize];

            using (Sha256Stream sha256 = new Sha256Stream())
            {
                sha256.AddData(message);
                sha256.CalculateHash(actualDigest);
            }

            CollectionAssert.AreEqual(expectedDigest, actualDigest);
        }

        [TestMethod]
        public void TestLongMessage()
        {
            ByteSpan message = Encoding.ASCII.GetBytes(
                new string('a', 1000000)
            );
            byte[] expectedDigest = Utils.HexToBytes(
                "cdc76e5c 9914fb92 81a1c7e2 84d73e67 f1809a48 a497200e 046d39cc c7112cd0"
            );
            byte[] actualDigest = new byte[Sha256Stream.DigestSize];

            using (Sha256Stream sha256 = new Sha256Stream())
            {
                sha256.AddData(message);
                sha256.CalculateHash(actualDigest);
            }

            CollectionAssert.AreEqual(expectedDigest, actualDigest);
        }
    }
}
