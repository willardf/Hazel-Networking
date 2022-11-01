using Hazel.Crypto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Hazel.Dtls
{
    /// <summary>
    /// Current state of handshake sequence
    /// </summary>
    internal enum HandshakeState
    {
        ExpectingHello,
        ExpectingClientKeyExchange,
        ExpectingChangeCipherSpec,
        ExpectingFinish
    }

    /// <summary>
    /// State to manage the current epoch `N`
    /// </summary>
    internal struct CurrentEpoch
    {
        public Udp.FewerThreads.ConnectionId ConnectionId;
        public long NextOutgoingSequence;

        public ulong NextExpectedSequence;
        public ulong PreviousSequenceWindowBitmask;

        public IRecordProtection MasterRecordProtection { get; private set; }
        public IRecordProtection PreviousRecordProtection { get; private set; }

        [ThreadStatic]
        private ConcurrentBag<IRecordProtection> allRecordProtections;

        // Need to keep these around so we can re-transmit our
        // last handshake record flight
        public ByteSpan ExpectedClientFinishedVerification;
        public ByteSpan ServerFinishedVerification;
        public ulong NextOutgoingSequenceForPreviousEpoch;

        public void Init()
        {
            ByteSpan block = new byte[2 * Finished.Size];
            this.ServerFinishedVerification = block.Slice(0, Finished.Size);
            this.ExpectedClientFinishedVerification = block.Slice(Finished.Size, Finished.Size);
            this.allRecordProtections = new ConcurrentBag<IRecordProtection>();
        }

        public void SetRecordProtection(IRecordProtection newProtection)
        {
            this.PreviousRecordProtection = this.MasterRecordProtection;
            this.MasterRecordProtection = newProtection;
            while (this.allRecordProtections.TryTake(out var i))
            {
                i.Dispose();
            }

            if (newProtection == NullRecordProtection.Instance)
            {
                this.PreviousRecordProtection = null;
                return;
            }

            for (int i = 0; i < 4; ++i)
            {
                this.allRecordProtections.Add(newProtection.Duplicate());
            }
        }

        public void EncryptServerPlaintext_ThreadSafe(ByteSpan output, ByteSpan input, ref Record record)
        {
        tryagain:
            var master = this.MasterRecordProtection;
            if (!this.allRecordProtections.TryTake(out var local))
            {
                local = master.Duplicate();
            }

            if (local.Id != master.Id)
            {
                local.Dispose();
                goto tryagain;
            }

            local.EncryptServerPlaintext(output, input, ref record);

            if (local != NullRecordProtection.Instance)
            {
                this.allRecordProtections.Add(local);
            }
        }

        public void DisposeThreadStatics()
        {
            while (this.allRecordProtections.TryTake(out var i))
            {
                i.Dispose();
            }
        }
    }

    /// <summary>
    /// State to manage the transition from the current
    /// epoch `N` to epoch `N+1`
    /// </summary>
    internal struct NextEpoch
    {
        public ushort Epoch;

        public HandshakeState State;
        public CipherSuite SelectedCipherSuite;

        public ulong NextOutgoingSequence;

        public IHandshakeCipherSuite Handshake;
        public IRecordProtection RecordProtection;

        public ByteSpan ClientRandom;
        public ByteSpan ServerRandom;

        public Sha256Stream VerificationStream;

        public ByteSpan ClientVerification;
        public ByteSpan ServerVerification;
    }
}
