using System.IO;

namespace Hazel
{
    ///
    public static class BinaryWriterExtensions
    {
        ///
        public static void WritePacked(this BinaryWriter writer, uint value)
        {
            do
            {
                byte b = (byte)(value & 0xFF);
                if (value >= 0x80)
                {
                    b |= 0x80;
                }

                writer.Write(b);
                value >>= 7;
            } while (value > 0);
        }

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
        public static void WriteBytesFull(this BinaryWriter writer, byte[] bytes)
        {
            writer.WritePacked((uint)bytes.Length);
            writer.Write(bytes);
        }

        ///
        public static byte[] ReadBytesAndSize(this BinaryReader reader)
        {
            int len = (int)reader.ReadPackedUInt32();
            return reader.ReadBytes(len);
        }
    }
}