using System.IO;

namespace Hazel
{
    ///
    public static class BinaryReaderExtensions
    {
        ///
        public static uint ReadPackedUInt32(this BinaryReader reader)
        {
            bool readMore = true;
            int shift = 0;
            uint output = 0;

            while (readMore)
            {
                byte b = reader.ReadByte();
                if (b >= 0x80)
                {
                    readMore = true;
                    b ^= 0x80;
                }
                else
                {
                    readMore = false;
                }

                output |= (uint)(b << shift);
                shift += 7;
            }

            return output;
        }

        ///
        public static int ReadPackedInt32(this BinaryReader reader)
        {
            return (int)reader.ReadPackedUInt32();
        }

        ///
        public static byte[] ReadBytesAndSize(this BinaryReader reader)
        {
            int len = (int)reader.ReadPackedUInt32();
            return reader.ReadBytes(len);
        }
    }
}