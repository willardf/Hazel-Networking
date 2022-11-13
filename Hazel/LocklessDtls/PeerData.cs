using Hazel.Crypto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Hazel.Dtls
{

    /// <summary>
    /// Per-peer state
    /// </summary>
    internal sealed class PeerData : IDisposable
    {
        public ushort Epoch;
        public bool CanHandleApplicationData;

        public HazelDtlsSessionInfo Session;

        public CurrentEpoch CurrentEpoch;
        public NextEpoch NextEpoch;

        public readonly List<ByteSpan> QueuedApplicationDataMessage = new List<ByteSpan>();
        public readonly ConcurrentBag<MessageReader> ApplicationData = new ConcurrentBag<MessageReader>();
        public readonly ProtocolVersion ProtocolVersion;

        public DateTime StartOfNegotiation;

        public PeerData(ulong nextExpectedSequenceNumber, ProtocolVersion protocolVersion)
        {
            ByteSpan block = new byte[2 * Finished.Size];
            this.CurrentEpoch.ServerFinishedVerification = block.Slice(0, Finished.Size);
            this.CurrentEpoch.ExpectedClientFinishedVerification = block.Slice(Finished.Size, Finished.Size);
            this.ProtocolVersion = protocolVersion;

            ResetPeer(nextExpectedSequenceNumber);
        }

        public void ResetPeer(ulong nextExpectedSequenceNumber)
        {
            Dispose();

            this.Epoch = 0;
            this.CanHandleApplicationData = false;
            this.QueuedApplicationDataMessage.Clear();

            this.CurrentEpoch.NextOutgoingSequence = 2; // Account for our ClientHelloVerify
            this.CurrentEpoch.NextExpectedSequence = nextExpectedSequenceNumber;
            this.CurrentEpoch.PreviousSequenceWindowBitmask = 0;
            this.CurrentEpoch.RecordProtection = NullRecordProtection.Instance;
            this.CurrentEpoch.PreviousRecordProtection = null;
            this.CurrentEpoch.ServerFinishedVerification.SecureClear();
            this.CurrentEpoch.ExpectedClientFinishedVerification.SecureClear();

            this.NextEpoch.State = HandshakeState.ExpectingHello;
            this.NextEpoch.RecordProtection = null;
            this.NextEpoch.Handshake = null;
            this.NextEpoch.ClientRandom = new byte[Random.Size];
            this.NextEpoch.ServerRandom = new byte[Random.Size];
            this.NextEpoch.VerificationStream = new Sha256Stream();
            this.NextEpoch.ClientVerification = new byte[Finished.Size];
            this.NextEpoch.ServerVerification = new byte[Finished.Size];

            this.StartOfNegotiation = DateTime.UtcNow;
        }

        public void Dispose()
        {
            this.CurrentEpoch.RecordProtection?.Dispose();
            this.CurrentEpoch.PreviousRecordProtection?.Dispose();
            this.NextEpoch.RecordProtection?.Dispose();
            this.NextEpoch.Handshake?.Dispose();
            this.NextEpoch.VerificationStream?.Dispose();

            while (this.ApplicationData.TryTake(out var msg))
            {
                try
                {
                    msg.Recycle();
                }
                catch { }
            }
        }
    }

}
