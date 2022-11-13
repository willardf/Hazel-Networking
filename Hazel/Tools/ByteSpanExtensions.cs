namespace Hazel
{
    /// <summary>
    /// Extension functions for (en/de)coding integer values
    /// </summary>
    public static class ByteSpanBigEndianExtensions
    {
        // Write a 16-bit integer in big-endian format to output[0..2)
        public static void WriteBigEndian16(this ByteSpan output, ushort value, int offset = 0)
        {
            output[offset + 0] = (byte)(value >> 8);
            output[offset + 1] = (byte)(value >> 0);
        }

        // Write a 24-bit integer in big-endian format to output[0..3)
        public static void WriteBigEndian24(this ByteSpan output, uint value, int offset = 0)
        {
            output[offset + 0] = (byte)(value >> 16);
            output[offset + 1] = (byte)(value >> 8);
            output[offset + 2] = (byte)(value >> 0);
        }

        // Write a 32-bit integer in big-endian format to output[0..4)
        public static void WriteBigEndian32(this ByteSpan output, uint value, int offset)
        {
            output[offset + 0] = (byte)(value >> 24);
            output[offset + 1] = (byte)(value >> 16);
            output[offset + 2] = (byte)(value >> 8);
            output[offset + 3] = (byte)(value >> 0);
        }

        // Write a 48-bit integer in big-endian format to output[0..6)
        public static void WriteBigEndian48(this ByteSpan output, ulong value, int offset = 0)
        {
            output[offset + 0] = (byte)(value >> 40);
            output[offset + 1] = (byte)(value >> 32);
            output[offset + 2] = (byte)(value >> 24);
            output[offset + 3] = (byte)(value >> 16);
            output[offset + 4] = (byte)(value >> 8);
            output[offset + 5] = (byte)(value >> 0);
        }

        // Write a 64-bit integer in big-endian format to output[0..8)
        public static void WriteBigEndian64(this ByteSpan output, ulong value, int offset = 0)
        {
            output[offset + 0] = (byte)(value >> 56);
            output[offset + 1] = (byte)(value >> 48);
            output[offset + 2] = (byte)(value >> 40);
            output[offset + 3] = (byte)(value >> 32);
            output[offset + 4] = (byte)(value >> 24);
            output[offset + 5] = (byte)(value >> 16);
            output[offset + 6] = (byte)(value >> 8);
            output[offset + 7] = (byte)(value >> 0);
        }

        // Read a 16-bit integer in big-endian format from input[0..2)
        public static ushort ReadBigEndian16(this ByteSpan input, int offset = 0)
        {
            ushort value = 0;
            value |= (ushort)(input[offset + 0] << 8);
            value |= (ushort)(input[offset + 1] << 0);
            return value;
        }

        // Read a 24-bit integer in big-endian format from input[0..3)
        public static uint ReadBigEndian24(this ByteSpan input, int offset = 0)
        {
            uint value = 0;
            value |= (uint)input[offset + 0] << 16;
            value |= (uint)input[offset + 1] <<  8;
            value |= (uint)input[offset + 2] <<  0;
            return value;
        }

        // Read a 48-bit integer in big-endian format from input[0..3)
        public static ulong ReadBigEndian48(this ByteSpan input, int offset = 0)
        {
            ulong value = 0;
            value |= (ulong)input[offset + 0] << 40;
            value |= (ulong)input[offset + 1] << 32;
            value |= (ulong)input[offset + 2] << 24;
            value |= (ulong)input[offset + 3] << 16;
            value |= (ulong)input[offset + 4] <<  8;
            value |= (ulong)input[offset + 5] <<  0;
            return value;
        }
    }

    public static class ByteSpanLittleEndianExtensions
    {
        // Read a 24-bit integer in little-endian format from input[0..3)
        public static uint ReadLittleEndian24(this ByteSpan input, int offset = 0)
        {
            uint value = 0;
            value |= (uint)input[offset + 0];
            value |= (uint)input[offset + 1] << 8;
            value |= (uint)input[offset + 2] << 16;
            return value;
        }

        // Read a 24-bit integer in little-endian format from input[0..4)
        public static uint ReadLittleEndian32(this ByteSpan input, int offset = 0)
        {
            uint value = 0;
            value |= (uint)input[offset + 0];
            value |= (uint)input[offset + 1] << 8;
            value |= (uint)input[offset + 2] << 16;
            value |= (uint)input[offset + 3] << 24;
            return value;
        }

        /// <summary>
        /// Reuse an existing span if there is enough space,
        /// otherwise allocate new storage
        /// </summary>
        /// <param name="source">
        /// Source span we should attempt to reuse
        /// </param>
        /// <param name="requiredSize">Required size (bytes)</param>
        public static ByteSpan ReuseSpanIfPossible(this ByteSpan source, int requiredSize)
        {
            if (source.Length >= requiredSize)
            {
                return source.Slice(0, requiredSize);
            }

            return new byte[requiredSize];
        }

    }
}
