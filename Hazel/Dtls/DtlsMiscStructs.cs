using Hazel.Crypto;
using System;
using System.Collections.Generic;

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
        public long NextOutgoingSequence;

        public ulong NextExpectedSequence;
        public ulong PreviousSequenceWindowBitmask;

        public IRecordProtection MasterRecordProtection;
        public IRecordProtection PreviousRecordProtection;

        [ThreadStatic]
        private IRecordProtection recordProtection;
        private List<IRecordProtection> allRecordProtections;

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
            this.allRecordProtections = new List<IRecordProtection>();
        }

        public void EncryptServerPlaintext_ThreadSafe(ByteSpan output, ByteSpan input, ref Record record)
        {
            if (this.recordProtection == null
                || this.recordProtection.Id != this.MasterRecordProtection.Id)
            {
                if (this.recordProtection != null)
                {
                    this.recordProtection.Dispose();
                    lock (this.allRecordProtections)
                    {
                        this.allRecordProtections.Remove(this.recordProtection);
                    }
                }

                this.recordProtection = this.MasterRecordProtection.Duplicate();
                lock (this.allRecordProtections)
                {
                    this.allRecordProtections.Add(this.recordProtection);
                }
            }

            this.recordProtection.EncryptServerPlaintext(output, input, ref record);
        }

        public void DisposeThreadStatics()
        {
            lock (this.allRecordProtections)
            {
                foreach (var i in this.allRecordProtections)
                {
                    i.Dispose();
                }

                this.allRecordProtections.Clear();
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
