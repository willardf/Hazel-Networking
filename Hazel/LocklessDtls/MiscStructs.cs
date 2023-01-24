using Hazel.Crypto;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hazel.Dtls
{
    /// <summary>
    /// Current state of handshake sequence
    /// </summary>
    enum HandshakeState
    {
        ExpectingHello,
        ExpectingClientKeyExchange,
        ExpectingChangeCipherSpec,
        ExpectingFinish
    }

    /// <summary>
    /// State to manage the current epoch `N`
    /// </summary>
    struct CurrentEpoch
    {
        public ulong NextOutgoingSequence;

        public ulong NextExpectedSequence;
        public ulong PreviousSequenceWindowBitmask;

        public IRecordProtection RecordProtection;
        public IRecordProtection PreviousRecordProtection;

        // Need to keep these around so we can re-transmit our
        // last handshake record flight
        public ByteSpan ExpectedClientFinishedVerification;
        public ByteSpan ServerFinishedVerification;
        public ulong NextOutgoingSequenceForPreviousEpoch;
    }

    /// <summary>
    /// State to manage the transition from the current
    /// epoch `N` to epoch `N+1`
    /// </summary>
    struct NextEpoch
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
