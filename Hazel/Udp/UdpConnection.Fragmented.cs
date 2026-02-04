using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace Hazel.Udp
{
    public partial class UdpConnection
    {
        /// <summary>
        /// Maximum possible UDP header size - 60-byte IP header + 8-byte UDP header.
        /// </summary>
        public const ushort MaxUdpHeaderSize = 68;

        /// <summary>
        /// Popular MTU values used for quick MTU discovery.
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
        /// MTU of this connection.
        /// </summary>
        public int Mtu => ForcedMtu ?? _mtu;

        /// <summary>
        /// Forced MTU, overrides the discovered MTU.
        /// </summary>
        public int? ForcedMtu { get; set; } = null;

        /// <summary>
        ///     Called when the MTU changes.
        /// </summary>
        public event Action MtuChanged;

        private byte _mtuIndex;

        private readonly ConcurrentDictionary<ushort, FragmentedMessage> _fragmentedMessagesReceived = new ConcurrentDictionary<ushort, FragmentedMessage>();
        private volatile int _lastFragmentedId;

        protected void StartMtuDiscovery()
        {
            if (!this.UseMtuDiscovery)
            {
                return;
            }

            if (!this.FragmentationEnabled)
            {
                return;
            }

            if (ForcedMtu.HasValue)
            {
                return;
            }

            MtuTest(_mtuIndex);
        }

        private void MtuTest(byte index)
        {
            var mtu = PossibleMtu[index];
            var failed = false;

            using SmartBuffer buffer = this.bufferPool.GetObject();
            buffer.Length = mtu;
            buffer[0] = (byte)UdpSendOption.MtuTest;

            // The MTU test message has an extra 2-byte MTU marker at the end. If it arrives, we can verify it.
            // Because the underlying socket has DontFragment=true, an oversized packet should fail fast with a SocketException.
            var id = AttachReliableID(buffer, 1, buffer.Length, () =>
            {
                if (failed) return;
                MtuOk(index);
            });

            buffer[mtu - 2] = (byte)mtu;
            buffer[mtu - 1] = (byte)(mtu >> 8);

            WriteBytesToConnection(buffer, buffer.Length, (SocketException _) =>
            {
                failed = true;
                CancelReliableMessageId(id);

                if (index == 0)
                {
                    DisconnectInternal(HazelInternalErrors.ConnectionDisconnected, "Connection MTU is lower than the minimum");
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

        // UdpSendOption.Fragment + ReliableID (2) + MessageId (2) + FragmentsCount (1) + FragmentId (1)
        private const byte FragmentHeaderSize = sizeof(byte) + sizeof(ushort) + sizeof(ushort) + sizeof(byte) + sizeof(byte);

        /// <summary>
        /// Fragments and sends a reliable message.
        /// </summary>
        protected void FragmentedSend(byte sendOption, byte[] data, Action ackCallback = null)
        {
            var length = data.Length + 1; // +1 for sendOption stored in the first fragment payload

            var id = (ushort)Interlocked.Increment(ref _lastFragmentedId);
            var fragmentSize = Mtu;
            var fragmentDataSize = fragmentSize - FragmentHeaderSize;

            if (fragmentDataSize <= 1)
            {
                throw new HazelException("MTU is too low to support fragmentation");
            }

            var fragmentsCount = (int)Math.Ceiling(length / (double)fragmentDataSize);
            if (fragmentsCount > byte.MaxValue)
            {
                throw new HazelException("Too many fragments");
            }

            var acksReceived = 0;

            for (byte i = 0; i < fragmentsCount; i++)
            {
                var dataLength = Math.Min(fragmentDataSize, length - fragmentDataSize * i);

                using SmartBuffer buffer = this.bufferPool.GetObject();
                buffer.Length = dataLength + FragmentHeaderSize;

                buffer[0] = (byte)UdpSendOption.Fragment;

                AttachReliableID(buffer, 1, buffer.Length, () =>
                {
                    if (Interlocked.Increment(ref acksReceived) >= fragmentsCount)
                    {
                        ackCallback?.Invoke();
                    }
                });

                buffer[3] = (byte)id;
                buffer[4] = (byte)(id >> 8);

                buffer[5] = (byte)fragmentsCount;
                buffer[6] = i;

                var includingHeader = i == 0;
                if (includingHeader)
                {
                    buffer[7] = sendOption;
                }

                Buffer.BlockCopy(
                    data,
                    fragmentDataSize * i - (includingHeader ? 0 : 1),
                    (byte[])buffer,
                    FragmentHeaderSize + (includingHeader ? 1 : 0),
                    dataLength - (includingHeader ? 1 : 0));

                WriteBytesToConnection(buffer, buffer.Length);

                // Count user payload bytes only (exclude sendOption stored in the first fragment)
                Statistics.LogFragmentedSend(dataLength - (includingHeader ? 1 : 0));
            }
        }

        protected void FragmentMessageReceive(MessageReader messageReader, int bytesReceived)
        {
            var isNew = ProcessReliableReceive(messageReader.Buffer, 1, out _);

            messageReader.Position = 3;
            var fragmentedMessageId = messageReader.ReadUInt16();
            var fragmentsCount = messageReader.ReadByte();
            var fragmentId = messageReader.ReadByte();

            if (fragmentsCount <= 0 || fragmentId >= fragmentsCount)
            {
                return;
            }

            var fragmentPayloadLen = bytesReceived - messageReader.Position;
            var userBytes = fragmentPayloadLen - (fragmentId == 0 ? 1 : 0);
            Statistics.LogFragmentedReceive(userBytes, bytesReceived);

            if (!isNew)
            {
                return;
            }

            if (!_fragmentedMessagesReceived.TryGetValue(fragmentedMessageId, out var fragmentedMessage))
            {
                lock (_fragmentedMessagesReceived)
                {
                    if (!_fragmentedMessagesReceived.TryGetValue(fragmentedMessageId, out fragmentedMessage))
                    {
                        if (!_fragmentedMessagesReceived.TryAdd(fragmentedMessageId, fragmentedMessage = new FragmentedMessage(fragmentsCount)))
                        {
                            throw new HazelException("Failed to add fragmented message");
                        }
                    }
                }
            }

            lock (fragmentedMessage)
            {
                if (fragmentedMessage.Fragments[fragmentId] != null)
                {
                    return;
                }

                var buffer = new byte[fragmentPayloadLen];
                Buffer.BlockCopy(messageReader.Buffer, messageReader.Position, buffer, 0, fragmentPayloadLen);
                fragmentedMessage.AddFragment(fragmentId, buffer);

                if (fragmentedMessage.IsFinished)
                {
                    var reconstructed = fragmentedMessage.Reconstruct();
                    var sendOption = (SendOption)reconstructed.Buffer[0];
                    InvokeDataReceived(sendOption, reconstructed, 1, fragmentedMessage.Size);

                    _fragmentedMessagesReceived.TryRemove(fragmentedMessageId, out _);
                }
            }
        }

        protected class FragmentedMessage
        {
            public int FragmentsCount { get; }
            public int FragmentsReceived { get; private set; }
            public int Size { get; private set; }
            public byte[][] Fragments { get; }
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

            public MessageReader Reconstruct()
            {
                if (!IsFinished)
                {
                    throw new HazelException("Can't reconstruct a FragmentedMessage until all fragments are received");
                }

                var buffer = MessageReader.GetSized(Size);
                buffer.Length = Size;

                var offset = 0;
                for (var i = 0; i < FragmentsCount; i++)
                {
                    var data = Fragments[i];
                    Buffer.BlockCopy(data, 0, buffer.Buffer, offset, data.Length);
                    offset += data.Length;
                }

                return buffer;
            }
        }
    }
}
