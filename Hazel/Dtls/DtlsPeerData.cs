using Hazel.Crypto;
using Hazel.Udp.FewerThreads;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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

        public ConnectionId ConnectionId;

        public readonly ConcurrentQueue<ByteSpan> QueuedApplicationDataMessage = new ConcurrentQueue<ByteSpan>();
        public readonly ConcurrentBag<MessageReader> ApplicationData = new ConcurrentBag<MessageReader>();
        public readonly ProtocolVersion ProtocolVersion;

        public DateTime StartOfNegotiation;

        public PeerData(ConnectionId connectionId, ulong nextExpectedSequenceNumber, ProtocolVersion protocolVersion)
        {
            this.CurrentEpoch.Init();
            this.ProtocolVersion = protocolVersion;

            ResetPeer(connectionId, nextExpectedSequenceNumber);
        }

        public void ResetPeer(ConnectionId connectionId, ulong nextExpectedSequenceNumber)
        {
            Dispose();

            this.Epoch = 0;
            this.CanHandleApplicationData = false;
            while (this.QueuedApplicationDataMessage.TryDequeue(out _));

            this.CurrentEpoch.NextOutgoingSequence = 2; // Account for our ClientHelloVerify
            this.CurrentEpoch.NextExpectedSequence = nextExpectedSequenceNumber;
            this.CurrentEpoch.PreviousSequenceWindowBitmask = 0;
            this.CurrentEpoch.MasterRecordProtection = NullRecordProtection.Instance;
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

            this.ConnectionId = connectionId;

            this.StartOfNegotiation = DateTime.UtcNow;
        }

        public void Dispose()
        {
            this.CurrentEpoch.MasterRecordProtection?.Dispose();
            this.CurrentEpoch.PreviousRecordProtection?.Dispose();
            this.CurrentEpoch.DisposeThreadStatics();

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
