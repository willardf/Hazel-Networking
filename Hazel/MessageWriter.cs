using System;
using System.IO;

namespace Hazel
{
    ///
    public class MessageWriter : IRecyclable
    {
        public static int BufferSize = 64000;
        private static readonly ObjectPool<MessageWriter> objectPool = new ObjectPool<MessageWriter>(() => new MessageWriter(BufferSize));

        internal byte[] Buffer;
        internal MemoryStream Stream;
        public readonly BinaryWriter Writer;

        public SendOption SendOption { get; private set; }

        private long lastMessageStart;

        ///
        public MessageWriter(int bufferSize)
        {
            this.Buffer = new byte[bufferSize];
            this.Stream = new MemoryStream(this.Buffer, true);
            this.Writer = new BinaryWriter(this.Stream);
        }

        ///
        /// <param name="sendOption">The option specifying how the message should be sent.</param>
        public static MessageWriter Get(SendOption sendOption = SendOption.None)
        {
            var output = objectPool.GetObject();
            output.SendOption = sendOption;

            switch (sendOption)
            {
                case SendOption.None:
                    output.Buffer[0] = (byte)sendOption;
                    output.Stream.Position = 1; // Type
                    break;
                case SendOption.Reliable:
                    output.Buffer[0] = (byte)sendOption;
                    output.Stream.Position = 3; // Type + ID
                    break;
                case SendOption.FragmentedReliable:
                    throw new NotImplementedException("Sry bruh");
            }

            return output;
        }

        ///
        public void StartMessage(byte typeFlag, uint targetObjId)
        {
            this.lastMessageStart = this.Stream.Position;
            this.Stream.Position = this.lastMessageStart + 2;

            this.Writer.Write(typeFlag);
            this.Writer.WritePacked(targetObjId);
        }

        ///
        public void EndMessage()
        {
            this.Writer.Flush();

            ushort length = (ushort)(this.Stream.Position - this.lastMessageStart);
            this.Buffer[this.lastMessageStart] = (byte)(length >> 8);
            this.Buffer[this.lastMessageStart + 1] = (byte)(length & 0xFF);
        }

        ///
        public void CancelMessage()
        {
            this.Writer.Flush();
            this.Stream.Position = this.lastMessageStart;
        }

        ///
        public void Recycle()
        {
            this.Writer.Flush();
            objectPool.PutObject(this);
        }
    }
}
