namespace Hazel.Dtls
{
    /// <summary>
    /// DTLS version constants
    /// </summary>
    public enum ProtocolVersion : ushort
    {
        /// <summary>
        /// DTLS 1.2
        /// </summary>
        DTLS1_2 = 0xFEFD,
    }

    /// <summary>
    /// DTLS record content type
    /// </summary>
    public enum ContentType : byte
    {
        ChangeCipherSpec = 20,
        Alert = 21,
        Handshake = 22,
        ApplicationData = 23,
    }

    /// <summary>
    /// Encode/decode DTLS record header
    /// </summary>
    public struct Record
    {
        public ContentType ContentType;
        public ushort Epoch;
        public ulong SequenceNumber;
        public ushort Length;

        public const int Size = 13;

        /// <summary>
        /// Parse a DTLS record from wire format
        /// </summary>
        /// <returns>True if we successfully parse the record header. Otherwise false</returns>
        public static bool Parse(out Record record, ByteSpan span)
        {
            record = new Record();

            if (span.Length < Size)
            {
                return false;
            }

            record.ContentType = (ContentType)span[0];
            ProtocolVersion version = (ProtocolVersion)span.ReadBigEndian16(1);
            record.Epoch = span.ReadBigEndian16(3);
            record.SequenceNumber = span.ReadBigEndian48(5);
            record.Length = span.ReadBigEndian16(11);

            if (version != ProtocolVersion.DTLS1_2)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Encode a DTLS record to wire format
        /// </summary>
        public void Encode(ByteSpan span)
        {
            span[0] = (byte)this.ContentType;
            span.WriteBigEndian16((ushort)ProtocolVersion.DTLS1_2, 1);
            span.WriteBigEndian16(this.Epoch, 3);
            span.WriteBigEndian48(this.SequenceNumber, 5);
            span.WriteBigEndian16(this.Length, 11);
        }
    }

    public struct ChangeCipherSpec
    {
        public const int Size = 1;

        enum Value : byte
        {
            ChangeCipherSpec = 1,
        }

        /// <summary>
        /// Parse a ChangeCipherSpec record from wire format
        /// </summary>
        /// <returns>
        /// True if we successfully parse the ChangeCipherSpec
        /// record. Otherwise, false.
        /// </returns>
        public static bool Parse(ByteSpan span)
        {
            if (span.Length != 1)
            {
                return false;
            }

            Value value = (Value)span[0];
            if (value != Value.ChangeCipherSpec)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Encode a ChangeCipherSpec record to wire format
        /// </summary>
        public static void Encode(ByteSpan span)
        {
            span[0] = (byte)Value.ChangeCipherSpec;
        }
    }
}
