using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Hazel.Udp
{
    public partial class UdpConnection
    {
        /// <summary>
        /// Maximum possible UDP header size - 60-byte IP header + 8-byte UDP header
        /// </summary>
        public const ushort MaxUdpHeaderSize = 68;

        /// <summary>
        /// Popular MTU values used for quick MTU discovery
        /// </summary>
        public static ushort[] PossibleMtu { get; } =
        {
            576 - MaxUdpHeaderSize, // RFC 1191
            1024,
            1460 - MaxUdpHeaderSize, // Google Cloud
            1492 - MaxUdpHeaderSize, // RFC 1042
            1500 - MaxUdpHeaderSize, // RFC 1191
        };

        private int _mtu = PossibleMtu[0];

        /// <summary>
        /// MTU of this connection
        /// </summary>
        public int Mtu => ForcedMtu ?? _mtu;

        /// <summary>
        /// Forced MTU, overrides the MTU
        /// </summary>
        public int? ForcedMtu { get; set; } = null;

        /// <summary>
        ///     Called when the MTU changes.
        /// </summary>
        public event Action MtuChanged;

        private byte _mtuIndex;

        private readonly Dictionary<ushort, FragmentedMessage> _fragmentedMessagesReceived = new Dictionary<ushort, FragmentedMessage>();
        private volatile int _lastFragmentedId;

        protected void StartMtuDiscovery()
        {
            MtuTest(_mtuIndex);
        }

        private void MtuTest(byte index)
        {
            var mtu = PossibleMtu[index];
            var failed = false;

            var buffer = new byte[mtu];
            buffer[0] = (byte)UdpSendOption.MtuTest;
            var id = AttachReliableID(buffer, 1, () =>
            {
                if (failed) return;
                MtuOk(index);
            });
            buffer[mtu - 2] = (byte)mtu;
            buffer[mtu - 1] = (byte)(mtu >> 8);

            WriteBytesToConnection(buffer, buffer.Length, () =>
            {
                failed = true;
                AcknowledgeMessageId(id);

                if (index == 0)
                {
                    DisconnectInternal(HazelInternalErrors.MtuTooLow, "Connection MTU is lower than the minimum");
                }
            });
        }

        private void MtuOk(byte index)
        {
            _mtuIndex = index;
            _mtu = PossibleMtu[index];
            MtuChanged?.Invoke();

            if (_mtuIndex < PossibleMtu.Length - 1)
            {
                MtuTest((byte)(index + 1));
            }
        }

        private void MtuTestMessageReceive(MessageReader message)
        {
            message.Position = message.Length - 2;
            var mtu = message.ReadUInt16();

            if (mtu != message.Length)
            {
                return;
            }

            ProcessReliableReceive(message.Buffer, 1, out _);
        }

        private const byte FragmentHeaderSize = sizeof(byte) + sizeof(ushort) + sizeof(ushort) + sizeof(byte) + sizeof(byte);

        protected void FragmentedSend(byte sendOption, byte[] data, Action ackCallback, bool includeHeader)
        {
            var length = includeHeader ? data.Length + 1 : data.Length;

            var id = (ushort)Interlocked.Increment(ref _lastFragmentedId);
            var fragmentSize = Mtu;
            var fragmentDataSize = fragmentSize - FragmentHeaderSize;
            var fragmentsCount = (int)Math.Ceiling(length / (double)fragmentDataSize);

            if (fragmentsCount >= byte.MaxValue)
            {
                throw new HazelException("Too many fragments");
            }

            var acksReceived = 0;

            for (byte i = 0; i < fragmentsCount; i++)
            {
                var dataLength = Math.Min(fragmentDataSize, length - fragmentDataSize * i);
                var buffer = new byte[dataLength + FragmentHeaderSize];

                buffer[0] = (byte)UdpSendOption.Fragment;

                AttachReliableID(buffer, 1, () =>
                {
                    acksReceived++;

                    if (acksReceived >= fragmentsCount)
                    {
                        ackCallback?.Invoke();
                    }
                });

                buffer[3] = (byte)id;
                buffer[4] = (byte)(id >> 8);

                buffer[5] = (byte)fragmentsCount;
                buffer[6] = i;

                var includingHeader = i == 0 && includeHeader;
                if (includingHeader)
                {
                    buffer[7] = sendOption;
                }

                Buffer.BlockCopy(data, fragmentDataSize * i - (includingHeader ? 0 : 1), buffer, FragmentHeaderSize + (includingHeader ? 1 : 0), dataLength - (includingHeader ? 1 : 0));

                WriteBytesToConnection(buffer, buffer.Length);
            }
        }

        protected void FragmentMessageReceive(MessageReader messageReader)
        {
            if (ProcessReliableReceive(messageReader.Buffer, 1, out _))
            {
                messageReader.Position += 3;

                var fragmentedMessageId = messageReader.ReadUInt16();
                var fragmentsCount = messageReader.ReadByte();
                var fragmentId = messageReader.ReadByte();

                if (fragmentsCount <= 0 || fragmentId >= fragmentsCount)
                {
                    return;
                }

                lock (_fragmentedMessagesReceived)
                {
                    if (!_fragmentedMessagesReceived.TryGetValue(fragmentedMessageId, out var fragmentedMessage))
                    {
                        _fragmentedMessagesReceived.Add(fragmentedMessageId, fragmentedMessage = new FragmentedMessage(fragmentsCount));
                    }

                    if (fragmentedMessage.Fragments[fragmentId] != null)
                    {
                        return;
                    }

                    var buffer = new byte[messageReader.Length - messageReader.Position];
                    Buffer.BlockCopy(messageReader.Buffer, messageReader.Position, buffer, 0, messageReader.Length - messageReader.Position);

                    fragmentedMessage.AddFragment(fragmentId, buffer);

                    if (fragmentedMessage.IsFinished)
                    {
                        var reconstructed = fragmentedMessage.Reconstruct();
                        InvokeDataReceived((SendOption)reconstructed[0], MessageReader.Get(reconstructed), 1, reconstructed.Length);

                        _fragmentedMessagesReceived.Remove(fragmentedMessageId);
                    }
                }
            }
        }

        protected class FragmentedMessage
        {
            /// <summary>
            ///     The total number of fragments expected.
            /// </summary>
            public int FragmentsCount { get; }

            /// <summary>
            ///     The number of fragments received.
            /// </summary>
            public int FragmentsReceived { get; private set; }

            /// <summary>
            ///     The total size of all fragments.
            /// </summary>
            public int Size { get; private set; }

            /// <summary>
            ///     The fragments received so far.
            /// </summary>
            public byte[][] Fragments { get; }

            /// <summary>
            ///     Whether all fragments were received.
            /// </summary>
            public bool IsFinished => FragmentsReceived == FragmentsCount;

            public FragmentedMessage(int fragmentsCount)
            {
                FragmentsCount = fragmentsCount;
                Fragments = new byte[fragmentsCount][];
            }

            public void AddFragment(byte id, byte[] fragment)
            {
                Fragments[id] = fragment;
                Size += fragment.Length;
                FragmentsReceived++;
            }

            public byte[] Reconstruct()
            {
                if (!IsFinished)
                {
                    throw new HazelException("Can't reconstruct a FragmentedMessage until all fragments are received");
                }

                var buffer = new byte[Size];

                var offset = 0;
                for (var i = 0; i < FragmentsCount; i++)
                {
                    var data = Fragments[i];
                    Buffer.BlockCopy(data, 0, buffer, offset, data.Length);
                    offset += data.Length;
                }

                return buffer;
            }
        }
    }
}
