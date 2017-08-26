using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hazel.Udp
{
    partial class UdpConnection
    {
        /// <summary>
        ///     The amount of data that can be put into a fragment.
        /// </summary>
        public int FragmentSize { get { return _fragmentSize; } }
        int _fragmentSize = 65507 - 1 - 2 - 2 - 2;

        /// <summary>
        ///     The last fragmented message ID that was written.
        /// </summary>
        volatile ushort lastFragmentIDAllocated;

        Dictionary<ushort, FragmentedMessage> fragmentedMessagesReceived = new Dictionary<ushort, FragmentedMessage>();

        /// <summary>
        ///     Sends a message fragmenting it as needed to pass over the network.
        /// </summary>
        /// <param name="sendOption">The send option the message was sent with.</param>
        /// <param name="data">The data of the message to send.</param>
        void FragmentedSend(byte[] data)
        {
            //Get an ID not used yet.
            ushort id = ++lastFragmentIDAllocated;      //TODO is extra code needed to manage loop around?

            for (ushort i = 0; i < Math.Ceiling(data.Length / (double)FragmentSize); i++)
            {
                byte[] buffer = new byte[Math.Min(data.Length - (FragmentSize * i), FragmentSize) + 7];

                //Add send option
                buffer[0] = i == 0 ? (byte)SendOption.FragmentedReliable : (byte)UdpSendOption.Fragment;

                //Add fragment message ID
                buffer[1] = (byte)((id >> 8) & 0xFF);
                buffer[2] = (byte)id;

                //Add length or fragment id
                if (i == 0)
                {
                    ushort fragments = (ushort)Math.Ceiling(data.Length / (double)FragmentSize);
                    buffer[3] = (byte)((fragments >> 8) & 0xFF);
                    buffer[4] = (byte)fragments;
                }
                else
                {
                    buffer[3] = (byte)((i >> 8) & 0xFF);
                    buffer[4] = (byte)i;
                }

                //Pass fragment to reliable send code to ensure it will arrive
                AttachReliableID(buffer, 5);

                //Copy data into fragment
                Buffer.BlockCopy(data, FragmentSize * i, buffer, 7, buffer.Length - 7);

                //Send
                WriteBytesToConnection(buffer);
            }
        }

        /// <summary>
        ///     Gets a message from those we've begun receiving or adds a new one.
        /// </summary>
        /// <param name="messageId">The Id of the message to find.</param>
        /// <returns></returns>
        FragmentedMessage GetFragmentedMessage(ushort messageId)
        {
            lock (fragmentedMessagesReceived)
            {
                FragmentedMessage message;
                if (fragmentedMessagesReceived.ContainsKey(messageId))
                {
                    message = fragmentedMessagesReceived[messageId];
                }
                else
                {
                    message = new FragmentedMessage();

                    fragmentedMessagesReceived.Add(messageId, message);
                }

                return message;
            }
        }

        /// <summary>
        ///     Handles a the start message of a fragmented message.
        /// </summary>
        /// <param name="buffer">The buffer received.</param>
        void FragmentedStartMessageReceive(byte[] buffer)
        {
            //Send to reliable code to send the acknowledgement
            if (!ProcessReliableReceive(buffer, 5))
                return;

            ushort id = (ushort)((buffer[1] << 8) + buffer[2]);

            ushort length = (ushort)((buffer[3] << 8) + buffer[4]);

            FragmentedMessage message;
            bool messageComplete;
            lock (fragmentedMessagesReceived)
            {
                message = GetFragmentedMessage(id);
                message.received.Add(new FragmentedMessage.Fragment(0, buffer, 7));
                message.noFragments = length;

                messageComplete = message.noFragments == message.received.Count;
            }

            if (messageComplete)
                FinalizeFragmentedMessage(message);
        }

        /// <summary>
        ///     Handles a fragment message of a fragmented message.
        /// </summary>
        /// <param name="buffer">The buffer received.</param>
        void FragmentedMessageReceive(byte[] buffer)
        {
            //Send to reliable code to send the acknowledgement
            if (!ProcessReliableReceive(buffer, 5))
                return;

            ushort id = (ushort)((buffer[1] << 8) + buffer[2]);

            ushort fragmentID = (ushort)((buffer[3] << 8) + buffer[4]);

            FragmentedMessage message;
            bool messageComplete;
            lock (fragmentedMessagesReceived)
            {
                message = GetFragmentedMessage(id);
                message.received.Add(new FragmentedMessage.Fragment(fragmentID, buffer, 7));

                messageComplete = message.noFragments == message.received.Count;
            }

            if (messageComplete)
                FinalizeFragmentedMessage(message);
        }

        /// <summary>
        ///     Finalizes a completed fragmented message and invokes message received events.
        /// </summary>
        /// <param name="message">The message received.</param>
        void FinalizeFragmentedMessage(FragmentedMessage message)
        {
            IEnumerable<FragmentedMessage.Fragment> orderedFragments = message.received.OrderBy((x) => x.fragmentID);

            byte[] completeData = new byte[(orderedFragments.Count() - 1) * FragmentSize + orderedFragments.Last().data.Length];
            int ptr = 0;
            foreach (FragmentedMessage.Fragment fragment in orderedFragments)
            {
                Buffer.BlockCopy(fragment.data, fragment.offset, completeData, ptr, fragment.data.Length - fragment.offset);
                ptr += fragment.data.Length - fragment.offset;
            }

            InvokeDataReceived(completeData, SendOption.FragmentedReliable);
        }

        /// <summary>
        ///     Holding class for the parts of a fragmented message so far received.
        /// </summary>
        private class FragmentedMessage
        {
            /// <summary>
            ///     The total number of fragments expected.
            /// </summary>
            public int noFragments = -1;

            /// <summary>
            ///     The fragments received so far.
            /// </summary>
            public List<Fragment> received = new List<Fragment>();

            public struct Fragment
            {
                public int fragmentID;
                public byte[] data;
                public int offset;

                public Fragment(int fragmentID, byte[] data, int offset)
                {
                    this.fragmentID = fragmentID;
                    this.data = data;
                    this.offset = offset;
                }
            }
        }
    }
}
