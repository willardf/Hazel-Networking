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

        private const byte FragmentHeaderSize = sizeof(byte) + sizeof(ushort) + sizeof(ushort) + sizeof(ushort);

        protected void FragmentedSend(byte[] data, Action ackCallback, bool includeHeader)
        {
            var id = (ushort)Interlocked.Increment(ref _lastFragmentedId);
            var fragmentSize = Mtu;
            var fragmentDataSize = fragmentSize - FragmentHeaderSize;
            var fragmentsCount = (int)Math.Ceiling(data.Length / (double)fragmentDataSize);

            if (fragmentsCount >= ushort.MaxValue)
            {
                throw new HazelException("Too many fragments");
            }

            for (ushort i = 0; i < fragmentsCount; i++)
            {
                var dataLength = Math.Min(fragmentDataSize, data.Length - fragmentDataSize * i);
                var buffer = new byte[dataLength + FragmentHeaderSize];

                buffer[0] = (byte)UdpSendOption.Fragment;

                AttachReliableID(buffer, 1);

                buffer[3] = (byte)fragmentsCount;
                buffer[4] = (byte)(fragmentsCount >> 8);

                buffer[5] = (byte)id;
                buffer[6] = (byte)(id >> 8);

                Buffer.BlockCopy(data, fragmentDataSize * i, buffer, FragmentHeaderSize, dataLength);

                WriteBytesToConnection(buffer, buffer.Length);
            }
        }

        protected void FragmentMessageReceive(MessageReader messageReader)
        {
            if (ProcessReliableReceive(messageReader.Buffer, 1, out var id))
            {
                messageReader.Position += 3;

                var fragmentsCount = messageReader.ReadUInt16();
                var fragmentedMessageId = messageReader.ReadUInt16();

                lock (_fragmentedMessagesReceived)
                {
                    if (!_fragmentedMessagesReceived.TryGetValue(fragmentedMessageId, out var fragmentedMessage))
                    {
                        _fragmentedMessagesReceived.Add(fragmentedMessageId, fragmentedMessage = new FragmentedMessage(fragmentsCount));
                    }

                    var buffer = new byte[messageReader.Length - messageReader.Position];
                    Buffer.BlockCopy(messageReader.Buffer, messageReader.Position, buffer, 0, messageReader.Length - messageReader.Position);

                    fragmentedMessage.Fragments.Add(new FragmentedMessage.Fragment(id, buffer));

                    if (fragmentedMessage.Fragments.Count == fragmentsCount)
                    {
                        var reconstructed = fragmentedMessage.Reconstruct();
                        InvokeDataReceived(MessageReader.Get(reconstructed), SendOption.Reliable);

                        _fragmentedMessagesReceived.Remove(id);
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
            ///     The fragments received so far.
            /// </summary>
            public HashSet<Fragment> Fragments { get; } = new HashSet<Fragment>();

            public byte[] Reconstruct()
            {
                if (Fragments.Count != FragmentsCount)
                {
                    throw new HazelException("Can't reconstruct a FragmentedMessage until all fragments are received");
                }

                var buffer = new byte[Fragments.Sum(x => x.Data.Length)];

                var offset = 0;
                foreach (var fragment in Fragments.OrderBy(fragment => fragment.Id))
                {
                    var data = fragment.Data;
                    Buffer.BlockCopy(data, 0, buffer, offset, data.Length);
                    offset += data.Length;
                }

                return buffer;
            }

            public FragmentedMessage(int fragmentsCount)
            {
                FragmentsCount = fragmentsCount;
            }

            public readonly struct Fragment
            {
                public int Id { get; }
                public byte[] Data { get; }

                public Fragment(int id, byte[] data)
                {
                    Id = id;
                    Data = data;
                }
            }
        }
    }
}
