using Hazel;
using Hazel.Crypto;
using Hazel.Udp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hazel.Dtls
{
    /// <summary>
    /// Connects to a UDP-DTLS server
    /// </summary>
    /// <inheritdoc />
    public class DtlsUnityConnection : UnityUdpClientConnection
    {
        /// <summary>
        /// Current state of the handshake sequence
        /// </summary>
        enum HandshakeState
        {
            Established,

            ExpectingServerHello,
            ExpectingCertificate,
            ExpectingServerKeyExchange,
            ExpectingServerHelloDone,
            ExpectingChangeCipherSpec,
            ExpectingFinished,

            Initializing,
        }

        /// <summary>
        /// State data for the current epoch
        /// </summary>
        struct CurrentEpoch
        {
            public ulong NextOutgoingSequence;

            public ulong NextExpectedSequence;
            public ulong PreviousSequenceWindowBitmask;

            public IRecordProtection RecordProtection;
        }

        struct FragmentRange
        {
            public int Offset;
            public int Length;
        }

        /// <summary>
        /// State data for the next epoch
        /// </summary>
        struct NextEpoch
        {
            public ushort Epoch;

            public HandshakeState State;

            public ulong NextOutgoingSequence;

            public DateTime NegotiationStartTime;
            public DateTime NextPacketResendTime;
            public int PacketResendCount;

            public CipherSuite SelectedCipherSuite;
            public IRecordProtection RecordProtection;
            public IHandshakeCipherSuite Handshake;
            public ByteSpan Cookie;
            public Sha256Stream VerificationStream;
            public RSA ServerPublicKey;

            public ByteSpan ClientRandom;
            public ByteSpan ServerRandom;

            public ByteSpan MasterSecret;
            public ByteSpan ServerVerification;

            public List<FragmentRange> CertificateFragments;
            public ByteSpan CertificatePayload;
        }

        struct QueuedAppData
        {
            public byte[] Bytes;
            public byte SendOption;
            public Action AckCallback;
        }

        private readonly object syncRoot = new object();
        private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();

        private ushort epoch;
        private CurrentEpoch currentEpoch;
        private NextEpoch nextEpoch;
        private TimeSpan handshakeResendTimeout = TimeSpan.FromMilliseconds(200);

        private readonly Queue<QueuedAppData> queuedApplicationData = new Queue<QueuedAppData>();

        private X509Certificate2Collection serverCertificates = new X509Certificate2Collection();

        /// <summary>
        /// Create a new instance of the DTLS connection
        /// </summary>
        /// <inheritdoc />
        public DtlsUnityConnection(ILogger logger, IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4)
            : base(logger, remoteEndPoint, ipMode)
        {
            this.nextEpoch.ServerRandom = new byte[Random.Size];
            this.nextEpoch.ClientRandom = new byte[Random.Size];
            this.nextEpoch.ServerVerification = new byte[Finished.Size];
            this.nextEpoch.CertificateFragments = new List<FragmentRange>();

            this.ResetConnectionState();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            lock (this.syncRoot)
            {
                this.ResetConnectionState();
            }
        }

        /// <summary>
        /// Set the list of valid server certificates
        /// </summary>
        /// <param name="certificateCollection">
        /// List of certificates of authentic servers
        /// </param>
        public void SetValidServerCertificates(X509Certificate2Collection certificateCollection)
        {
            lock (this.syncRoot)
            {
                foreach (X509Certificate2 certificate in certificateCollection)
                {
                    if (!(certificate.PublicKey.Key is RSA))
                    {
                        throw new ArgumentException("Certificate must be signed with an RSA key", nameof(certificateCollection));
                    }
                }

                this.serverCertificates = certificateCollection;
            }
        }

        /// <summary>
        /// Set the packet resend timer for handshake messages
        /// </summary>
        public void SetHandshakeResendTimeout(TimeSpan timeout)
        {
            lock (this.syncRoot)
            {
                this.handshakeResendTimeout = timeout;
            }
        }

        /// <summary>
        /// Reset existing connection state
        /// </summary>
        private void ResetConnectionState()
        {
            this.currentEpoch.NextOutgoingSequence = 1;
            this.currentEpoch.NextExpectedSequence = 1;
            this.currentEpoch.PreviousSequenceWindowBitmask = 0;
            this.currentEpoch.RecordProtection?.Dispose();
            this.currentEpoch.RecordProtection = NullRecordProtection.Instance;

            this.nextEpoch.Epoch = 1;
            this.nextEpoch.State = HandshakeState.Initializing;
            this.nextEpoch.NextOutgoingSequence = 1;
            this.nextEpoch.NegotiationStartTime = DateTime.MinValue;
            this.nextEpoch.NextPacketResendTime = DateTime.MinValue;
            this.nextEpoch.SelectedCipherSuite = CipherSuite.TLS_NULL_WITH_NULL_NULL;
            this.nextEpoch.RecordProtection?.Dispose();
            this.nextEpoch.RecordProtection = null;
            this.nextEpoch.Handshake?.Dispose();
            this.nextEpoch.Handshake = null;
            this.nextEpoch.Cookie = ByteSpan.Empty;
            this.nextEpoch.VerificationStream?.Dispose();
            this.nextEpoch.VerificationStream = new Sha256Stream();
            this.nextEpoch.ServerPublicKey = null;
            this.nextEpoch.ServerRandom.SecureClear();
            this.nextEpoch.ClientRandom.SecureClear();
            this.nextEpoch.MasterSecret.SecureClear();
            this.nextEpoch.ServerVerification.SecureClear();
            this.nextEpoch.CertificateFragments.Clear();
            this.nextEpoch.CertificatePayload = ByteSpan.Empty;
            
            this.epoch = 0;
            while (this.queuedApplicationData.TryDequeue(out _)) ;
        }

        /// <summary>
        /// Abort the existing connection and restart the process
        /// </summary>
        protected override void RestartConnection()
        {
            lock (this.syncRoot)
            {
                this.ResetConnectionState();
                this.nextEpoch.ClientRandom.FillWithRandom(this.random);
                this.SendClientHello();
            }

            base.RestartConnection();
        }

        /// <inheritdoc />
        protected override void ResendPacketsIfNeeded()
        {
            lock (this.syncRoot)
            {
                // Check if we need to resend handshake message
                if (this.nextEpoch.State != HandshakeState.Established)
                {
                    DateTime now = DateTime.UtcNow;
                    if (now >= this.nextEpoch.NextPacketResendTime)
                    {
                        double negotiationDurationMs = (now - this.nextEpoch.NegotiationStartTime).TotalMilliseconds;
                        this.nextEpoch.PacketResendCount++;

                        if ((this.ResendLimit > 0 && this.nextEpoch.PacketResendCount > this.ResendLimit)
                            || negotiationDurationMs > this.DisconnectTimeoutMs)
                        {
                            this.DisconnectInternal(HazelInternalErrors.DtlsNegotiationFailed, $"DTLS negotiation failed after {this.nextEpoch.PacketResendCount} resends ({(int)negotiationDurationMs} ms).");
                        }
                        else
                        {
                            switch (this.nextEpoch.State)
                            {
                                case HandshakeState.ExpectingServerHello:
                                case HandshakeState.ExpectingCertificate:
                                case HandshakeState.ExpectingServerKeyExchange:
                                case HandshakeState.ExpectingServerHelloDone:
                                    this.SendClientHello();
                                    break;

                                case HandshakeState.ExpectingChangeCipherSpec:
                                case HandshakeState.ExpectingFinished:
                                    this.SendClientKeyExchangeFlight(true);
                                    break;

                                case HandshakeState.Established:
                                default:
                                    break;
                            }
                        }
                    }
                }
            }

            base.ResendPacketsIfNeeded();
        }

        /// <summary>
        /// Flush any queued application data packets
        /// </summary>
        private void FlushQueuedApplicationData()
        {
            while (this.queuedApplicationData.TryDequeue(out var queuedData))
            {
                base.HandleSend(queuedData.Bytes, queuedData.SendOption, queuedData.AckCallback);
            }
        }

        /// <summary>
        /// Request from the application to write data to the DTLS
        /// stream. If appropriate, returns a byte span to send to
        /// the wire.
        /// </summary>
        /// <param name="bytes">Plaintext bytes to write</param>
        /// <param name="length">Length of the bytes to write</param>
        /// <returns>
        /// Encrypted data to put on the wire if appropriate,
        /// otherwise an empty span
        /// </returns>
        private ByteSpan WriteBytesToConnectionInternal(byte[] bytes, int length)
        {
            lock (this.syncRoot)
            {
                Record outgoinRecord = new Record();
                outgoinRecord.ContentType = ContentType.ApplicationData;
                outgoinRecord.Epoch = this.epoch;
                outgoinRecord.SequenceNumber = this.currentEpoch.NextOutgoingSequence;
                outgoinRecord.Length = (ushort)this.currentEpoch.RecordProtection.GetEncryptedSize(length);
                ++this.currentEpoch.NextOutgoingSequence;

                // Encode the record to wire format
                ByteSpan packet = new byte[Record.Size + outgoinRecord.Length];
                ByteSpan writer = packet;
                outgoinRecord.Encode(writer);
                writer = writer.Slice(Record.Size);
                new ByteSpan(bytes, 0, length).CopyTo(writer);

                // Protect the record
                this.currentEpoch.RecordProtection.EncryptClientPlaintext(
                    packet.Slice(Record.Size, outgoinRecord.Length),
                    packet.Slice(Record.Size, length),
                    ref outgoinRecord);

                return packet;
            }
        }

        protected override void HandleSend(byte[] data, byte sendOption, Action ackCallback = null)
        {
            lock (this.syncRoot)
            {
                // If we're negotiating a new epoch, queue data
                if (this.nextEpoch.State != HandshakeState.Established)
                {
                    this.queuedApplicationData.Enqueue(new QueuedAppData
                    {
                        Bytes = data,
                        SendOption = sendOption,
                        AckCallback = ackCallback
                    });

                    return;
                }
            }

            base.HandleSend(data, sendOption, ackCallback);
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnection(byte[] bytes, int length)
        {
            ByteSpan wireData = this.WriteBytesToConnectionInternal(bytes, length);
            if (wireData.Length > 0)
            {
                Debug.Assert(wireData.Offset == 0, "Got a non-zero write data offset");
                base.WriteBytesToConnection(wireData.GetUnderlyingArray(), wireData.Length);
            }
        }

        /// <inheritdoc />
        protected override void WriteBytesToConnectionSync(byte[] bytes, int length)
        {
            ByteSpan wireData = this.WriteBytesToConnectionInternal(bytes, length);
            if (wireData.Length > 0)
            {
                Debug.Assert(wireData.Offset == 0, "Got a non-zero write data offset");
                base.WriteBytesToConnectionSync(wireData.GetUnderlyingArray(), wireData.Length);
            }
        }

        /// <inheritdoc />
        protected internal override void HandleReceive(MessageReader reader, int bytesReceived)
        {
            ByteSpan message = new ByteSpan(reader.Buffer, reader.Offset + reader.Position, reader.BytesRemaining);
            lock (this.syncRoot)
            {
                this.HandleReceive(message);
            }

            reader.Recycle();
        }

        /// <summary>
        /// Handle an incoming datagram
        /// </summary>
        /// <param name="span">Bytes of the datagram</param>
        private void HandleReceive(ByteSpan span)
        {
            // Each incoming packet may contain multiple DTLS
            // records
            while (span.Length > 0)
            {
                Record record;
                if (!Record.Parse(out record, span))
                {
                    this.logger.WriteError("Dropping malformed record");
                    return;
                }
                span = span.Slice(Record.Size);

                if (span.Length < record.Length)
                {
                    this.logger.WriteError($"Dropping malformed record. Length({record.Length}) Available Bytes({span.Length})");
                    return;
                }

                ByteSpan recordPayload = span.Slice(0, record.Length);
                span = span.Slice(record.Length);

                // Early out and drop ApplicationData records
                if (record.ContentType == ContentType.ApplicationData && this.nextEpoch.State != HandshakeState.Established)
                {
                    this.logger.WriteError("Dropping ApplicationData record. Cannot process yet");
                    continue;
                }

                // Drop records from a different epoch
                if (record.Epoch != this.epoch)
                {
                    this.logger.WriteError($"Dropping bad-epoch record. RecordEpoch({record.Epoch}) Epoch({this.epoch})");
                    continue;
                }

                // Prevent replay attacks by dropping records
                // we've already processed
                int windowIndex = (int)(this.currentEpoch.NextExpectedSequence - record.SequenceNumber - 1);
                ulong windowMask = 1ul << windowIndex;
                if (record.SequenceNumber < this.currentEpoch.NextExpectedSequence)
                {
                    if (windowIndex >= 64)
                    {
                        this.logger.WriteError($"Dropping too-old record: Sequnce({record.SequenceNumber}) Expected({this.currentEpoch.NextExpectedSequence})");
                        continue;
                    }

                    if ((this.currentEpoch.PreviousSequenceWindowBitmask & windowMask) != 0)
                    {
                        this.logger.WriteError("Dropping duplicate record");
                        continue;
                    }
                }

                // Verify record authenticity
                int decryptedSize = this.currentEpoch.RecordProtection.GetDecryptedSize(recordPayload.Length);
                ByteSpan decryptedPayload = recordPayload.ReuseSpanIfPossible(decryptedSize);

                if (!this.currentEpoch.RecordProtection.DecryptCiphertextFromServer(decryptedPayload, recordPayload, ref record))
                {
                    this.logger.WriteError("Dropping non-authentic record");
                    return;
                }

                recordPayload = decryptedPayload;

                // Update out sequence number bookkeeping
                if (record.SequenceNumber >= this.currentEpoch.NextExpectedSequence)
                {
                    int windowShift = (int)(record.SequenceNumber + 1 - this.currentEpoch.NextExpectedSequence);
                    this.currentEpoch.PreviousSequenceWindowBitmask <<= windowShift;
                    this.currentEpoch.NextExpectedSequence = record.SequenceNumber + 1;
                }
                else
                {
                    this.currentEpoch.PreviousSequenceWindowBitmask |= windowMask;
                }

                switch (record.ContentType)
                {
                    case ContentType.ChangeCipherSpec:
                        if (this.nextEpoch.State != HandshakeState.ExpectingChangeCipherSpec)
                        {
                            this.logger.WriteError($"Dropping unexpected ChangeCipherSpec State({this.nextEpoch.State})");
                            break;
                        }
                        else if (this.nextEpoch.RecordProtection == null)
                        {
                            ///NOTE(mendsley): This _should_ not
                            /// happen on a well-formed client.
                            Debug.Assert(false, "How did we receive a ChangeCipherSpec message without a pending record protection instance?");
                            break;
                        }

                        if (!ChangeCipherSpec.Parse(recordPayload))
                        {
                            this.logger.WriteError("Dropping malformed ChangeCipherSpec message");
                            break;
                        }

                        // Migrate to the next epoch
                        this.epoch = this.nextEpoch.Epoch;
                        this.currentEpoch.RecordProtection = this.nextEpoch.RecordProtection;
                        this.currentEpoch.NextOutgoingSequence = this.nextEpoch.NextOutgoingSequence;
                        this.currentEpoch.NextExpectedSequence = 1;
                        this.currentEpoch.PreviousSequenceWindowBitmask = 0;

                        this.nextEpoch.State = HandshakeState.ExpectingFinished;
                        this.nextEpoch.SelectedCipherSuite = CipherSuite.TLS_NULL_WITH_NULL_NULL;
                        this.nextEpoch.RecordProtection = null;
                        this.nextEpoch.Handshake?.Dispose();
                        this.nextEpoch.Cookie = ByteSpan.Empty;
                        this.nextEpoch.VerificationStream.Reset();
                        this.nextEpoch.ServerPublicKey = null;
                        this.nextEpoch.ServerRandom.SecureClear();
                        this.nextEpoch.ClientRandom.SecureClear();
                        this.nextEpoch.MasterSecret.SecureClear();
                        break;

                    case ContentType.Alert:
                        this.logger.WriteError("Dropping unsupported alert record");
                        continue;

                    case ContentType.Handshake:
                        if (!ProcessHandshake(ref record, recordPayload))
                        {
                            return;
                        }
                        break;

                    case ContentType.ApplicationData:
                        // Forward data to the application
                        MessageReader reader = MessageReader.GetSized(recordPayload.Length);
                        reader.Length = recordPayload.Length;
                        recordPayload.CopyTo(reader.Buffer);

                        base.HandleReceive(reader, recordPayload.Length);
                        break;
                }
            }
        }

        /// <summary>
        /// Process an incoming Handshake protocol message
        /// </summary>
        /// <param name="record">Parent record</param>
        /// <param name="message">Record payload</param>
        /// <returns>
        /// True if further processing of the underlying datagram
        /// should be continues. Otherwise, false.
        /// </returns>
        private bool ProcessHandshake(ref Record record, ByteSpan message)
        {
            // Each record may have multiple Handshake messages
            while (message.Length > 0)
            {
                ByteSpan originalPayload = message;

                Handshake handshake;
                if (!Handshake.Parse(out handshake, message))
                {
                    this.logger.WriteError("Dropping malformed handshake message");
                    return false;
                }
                message = message.Slice(Handshake.Size);

                if (message.Length < handshake.Length)
                {
                    this.logger.WriteError($"Dropping malformed handshake message: AvailableBytes({message.Length}) Size({handshake.Length})");
                    return false;
                }

                originalPayload = originalPayload.Slice(0, (int)(Handshake.Size + handshake.Length));
                ByteSpan payload = originalPayload.Slice(Handshake.Size);
                message = message.Slice((int)handshake.Length);

                // We only support fragmented Certificate messages
                // from the server
                if (handshake.MessageType != HandshakeType.Certificate && (handshake.FragmentOffset != 0 || handshake.FragmentLength != handshake.Length))
                {
                    this.logger.WriteError($"Dropping fragmented handshake message Type({handshake.MessageType}) Offset({handshake.FragmentOffset}) FragmentLength({handshake.FragmentLength}) Length({handshake.Length})");
                    continue;
                }

                switch (handshake.MessageType)
                {
                    case HandshakeType.HelloVerifyRequest:
                        if (this.nextEpoch.State != HandshakeState.ExpectingServerHello)
                        {
                            this.logger.WriteError($"Dropping unexpected HelloVerifyRequest handshake message State({this.nextEpoch.State})");
                            continue;
                        }
                        else if (handshake.MessageSequence != 0)
                        {
                            this.logger.WriteError($"Dropping bad-sequence HelloVerifyRequest MessageSequence({handshake.MessageSequence})");
                            continue;
                        }

                        HelloVerifyRequest helloVerifyRequest;
                        if (!HelloVerifyRequest.Parse(out helloVerifyRequest, payload))
                        {
                            this.logger.WriteError("Dropping malformed HelloVerifyRequest handshake message");
                            continue;
                        }

                        // Save the cookie
                        this.nextEpoch.Cookie = new byte[helloVerifyRequest.Cookie.Length];
                        helloVerifyRequest.Cookie.CopyTo(this.nextEpoch.Cookie);

                        // Restart the handshake
                        this.nextEpoch.ClientRandom.FillWithRandom(this.random);
                        this.SendClientHello();
                        break;

                    case HandshakeType.ServerHello:
                        if (this.nextEpoch.State != HandshakeState.ExpectingServerHello)
                        {
                            this.logger.WriteError($"Dropping unexpected ServerHello handshake message State({this.nextEpoch.State})");
                            continue;
                        }
                        else if (handshake.MessageSequence != 1)
                        {
                            this.logger.WriteError($"Dropping bad-sequence ServerHello MessageSequence({handshake.MessageSequence})");
                            continue;
                        }

                        ServerHello serverHello;
                        if (!ServerHello.Parse(out serverHello, payload))
                        {
                            this.logger.WriteError("Dropping malformed ServerHello message");
                            continue;
                        }

                        switch (serverHello.CipherSuite)
                        {
                            case CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256:
                                this.nextEpoch.Handshake = new X25519EcdheRsaSha256(this.random);
                                break;

                            default:
                                this.logger.WriteError($"Dropping malformed ServerHello message. Unsupported CipherSuite({serverHello.CipherSuite})");
                                continue;
                        }

                        // Save server parameters
                        this.nextEpoch.SelectedCipherSuite = serverHello.CipherSuite;
                        serverHello.Random.CopyTo(this.nextEpoch.ServerRandom);
                        this.nextEpoch.State = HandshakeState.ExpectingCertificate;
                        this.nextEpoch.CertificateFragments.Clear();
                        this.nextEpoch.CertificatePayload = ByteSpan.Empty;

                        // Append ServerHelllo message to the verification stream
                        this.nextEpoch.VerificationStream.AddData(originalPayload);
                        break;

                    case HandshakeType.Certificate:
                        if (this.nextEpoch.State != HandshakeState.ExpectingCertificate)
                        {
                            this.logger.WriteError($"Dropping unexpected Certificate handshake message State({this.nextEpoch.State})");
                            continue;
                        }
                        else if (handshake.MessageSequence != 2)
                        {
                            this.logger.WriteError($"Dropping bad-sequence Certificate MessageSequence({handshake.MessageSequence})");
                            continue;
                        }

                        // If this is a fragmented message
                        if (handshake.FragmentLength != handshake.Length)
                        {
                            if (this.nextEpoch.CertificatePayload.Length != handshake.Length)
                            {
                                this.nextEpoch.CertificatePayload = new byte[handshake.Length];
                                this.nextEpoch.CertificateFragments.Clear();
                            }

                            // Add this fragment
                            payload.CopyTo(this.nextEpoch.CertificatePayload.Slice((int)handshake.FragmentOffset, (int)handshake.FragmentLength));
                            this.nextEpoch.CertificateFragments.Add(new FragmentRange {Offset = (int)handshake.FragmentOffset, Length = (int)handshake.FragmentLength });
                            this.nextEpoch.CertificateFragments.Sort((FragmentRange lhs, FragmentRange rhs) => {
                                return lhs.Offset.CompareTo(rhs.Offset);
                            });

                            // Have we completed the message?
                            int currentOffset = 0;
                            bool valid = true;
                            foreach (FragmentRange range in this.nextEpoch.CertificateFragments)
                            {
                                if (range.Offset != currentOffset)
                                {
                                    valid = false;
                                    break;
                                }

                                currentOffset += range.Length;
                            }

                            if (currentOffset != this.nextEpoch.CertificatePayload.Length)
                            {
                                valid = false;
                            }

                            // Still waiting on more fragments?
                            if (!valid)
                            {
                                continue;
                            }

                            // Replace the message payload, and continue
                            this.nextEpoch.CertificateFragments.Clear();
                            payload = this.nextEpoch.CertificatePayload;
                        }

                        X509Certificate2 certificate;
                        if (!Certificate.Parse(out certificate, payload))
                        {
                            this.logger.WriteError("Dropping malformed Certificate message");
                            continue;
                        }

                        // Verify the certificate is authenticate
                        if (!this.serverCertificates.Contains(certificate))
                        {
                            this.logger.WriteError("Dropping malformed Certificate message: Certificate not authentic");
                            continue;
                        }

                        RSA publicKey = certificate.PublicKey.Key as RSA;
                        if (publicKey == null)
                        {
                            this.logger.WriteError("Dropping malfomed Certificate message: Certificate is not RSA signed");
                            continue;
                        }

                        // Add the final Certificate message to the verification stream
                        Handshake fullCertificateHandhake = handshake;
                        fullCertificateHandhake.FragmentOffset = 0;
                        fullCertificateHandhake.FragmentLength = fullCertificateHandhake.Length;

                        byte[] serializedCertificateHandshake = new byte[Handshake.Size];
                        fullCertificateHandhake.Encode(serializedCertificateHandshake);
                        this.nextEpoch.VerificationStream.AddData(serializedCertificateHandshake);
                        this.nextEpoch.VerificationStream.AddData(payload);

                        this.nextEpoch.ServerPublicKey = publicKey;
                        this.nextEpoch.State = HandshakeState.ExpectingServerKeyExchange;
                        break;

                    case HandshakeType.ServerKeyExchange:
                        if (this.nextEpoch.State != HandshakeState.ExpectingServerKeyExchange)
                        {
                            this.logger.WriteError($"Dropping unexpected ServerKeyExchange handshake message State({this.nextEpoch.State})");
                            continue;
                        }
                        else if (this.nextEpoch.ServerPublicKey == null)
                        {
                            ///NOTE(mendsley): This _should_ not
                            /// happen on a well-formed client
                            Debug.Assert(false, "How are we processing a ServerKeyExchange message without a server public key?");

                            this.logger.WriteError($"Dropping unexpected ServerKeyExchange handshake message: No server public key");
                            continue;
                        }
                        else if (this.nextEpoch.Handshake == null)
                        {
                            ///NOTE(mendsley): This _should_ not
                            /// happen on a well-formed client
                            Debug.Assert(false, "How did we receive a ServerKeyExchange message without a handshake instance?");

                            this.logger.WriteError($"Dropping unexpected ServerKeyExchange handshake message: No key agreement interface");
                            continue;
                        }
                        else if (handshake.MessageSequence != 3)
                        {
                            this.logger.WriteError($"Dropping bad-sequence ServerKeyExchange MessageSequence({handshake.MessageSequence})");
                            continue;
                        }

                        ByteSpan sharedSecret = new byte[this.nextEpoch.Handshake.SharedKeySize()];
                        if (!this.nextEpoch.Handshake.VerifyServerMessageAndGenerateSharedKey(sharedSecret, payload, this.nextEpoch.ServerPublicKey))
                        {
                            this.logger.WriteError("Dropping malformed ServerKeyExchangeMessage");
                            return false;
                        }

                        // Generate the session master secret
                        ByteSpan randomSeed = new byte[2 * Random.Size];
                        this.nextEpoch.ClientRandom.CopyTo(randomSeed);
                        this.nextEpoch.ServerRandom.CopyTo(randomSeed.Slice(Random.Size));

                        const int MasterSecretSize = 48;
                        ByteSpan masterSecret = new byte[MasterSecretSize];
                        PrfSha256.ExpandSecret(
                              masterSecret
                            , sharedSecret
                            , PrfLabel.MASTER_SECRET
                            , randomSeed
                        );

                        // Create record protection for the upcoming epoch
                        switch (this.nextEpoch.SelectedCipherSuite)
                        {
                            case CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256:
                                this.nextEpoch.RecordProtection = new Aes128GcmRecordProtection(
                                      masterSecret
                                    , this.nextEpoch.ServerRandom
                                    , this.nextEpoch.ClientRandom
                                );
                                break;

                            default:
                                ///NOTE(mendsley): this _should_ not
                                /// happen on a well-formed client.
                                Debug.Assert(false, "SeverHello processing already approved this ciphersuite");

                                this.logger.WriteError($"Dropping malformed ServerKeyExchangeMessage: Could not create record protection");
                                return false;
                        }

                        this.nextEpoch.State = HandshakeState.ExpectingServerHelloDone;
                        this.nextEpoch.MasterSecret = masterSecret;

                        // Append ServerKeyExchange to the verification stream
                        this.nextEpoch.VerificationStream.AddData(originalPayload);
                        break;

                    case HandshakeType.ServerHelloDone:
                        if (this.nextEpoch.State != HandshakeState.ExpectingServerHelloDone)
                        {
                            this.logger.WriteError($"Dropping unexpected ServerHelloDone handshake message State({this.nextEpoch.State})");
                            continue;
                        }
                        else if (handshake.MessageSequence != 4)
                        {
                            this.logger.WriteError($"Dropping bad-sequence ServerHelloDone MessageSequence({handshake.MessageSequence})");
                            continue;
                        }

                        this.nextEpoch.State = HandshakeState.ExpectingChangeCipherSpec;

                        // Append ServerHelloDone to the verification stream
                        this.nextEpoch.VerificationStream.AddData(originalPayload);

                        this.SendClientKeyExchangeFlight(false);
                        break;

                    case HandshakeType.Finished:
                        if (this.nextEpoch.State != HandshakeState.ExpectingFinished)
                        {
                            this.logger.WriteError($"Dropping unexpected Finished handshake message State({this.nextEpoch.State})");
                            continue;
                        }
                        else if (payload.Length != Finished.Size)
                        {
                            this.logger.WriteError($"Dropping malformed Finished handshake message Size({payload.Length})");
                            continue;
                        }
                        else if (handshake.MessageSequence != 7)
                        {
                            this.logger.WriteError($"Dropping bad-sequence Finished MessageSequence({handshake.MessageSequence})");
                            continue;
                        }

                        // Verify the digest from the server
                        if (1 != Crypto.Const.ConstantCompareSpans(payload, this.nextEpoch.ServerVerification))
                        {
                            this.logger.WriteError("Dropping non-verified Finished handshake message");
                            return false;
                        }

                        ++this.nextEpoch.Epoch;
                        this.nextEpoch.State = HandshakeState.Established;
                        this.nextEpoch.NegotiationStartTime = DateTime.MinValue;
                        this.nextEpoch.NextPacketResendTime = DateTime.MinValue;
                        this.nextEpoch.ServerVerification.SecureClear();
                        this.nextEpoch.MasterSecret.SecureClear();

                        this.FlushQueuedApplicationData();
                        break;

                    // Drop messages we do not support
                    case HandshakeType.CertificateRequest:
                    case HandshakeType.HelloRequest:
                        this.logger.WriteError($"Dropping unsupported handshake message MessageType({handshake.MessageType})");
                        break;

                    // Drop messages that originate from the client
                    case HandshakeType.ClientHello:
                    case HandshakeType.ClientKeyExchange:
                    case HandshakeType.CertificateVerify:
                        this.logger.WriteError($"Dropping client handshake message MessageType({handshake.MessageType})");
                        break;
                }
            }

            return true;
        }

        /// <summary>
        /// Send (resend) a ClientHello message to the server
        /// </summary>
        protected virtual void SendClientHello()
        {
            // Reset our verification stream
            this.nextEpoch.VerificationStream.Reset();

            // Describe our ClientHello flight
            ClientHello clientHello = new ClientHello();
            clientHello.Random = this.nextEpoch.ClientRandom;
            clientHello.Cookie = this.nextEpoch.Cookie;
            clientHello.CipherSuites = new byte[2];
            clientHello.CipherSuites.WriteBigEndian16((ushort)CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256);
            clientHello.SupportedCurves = new byte[2];
            clientHello.SupportedCurves.WriteBigEndian16((ushort)NamedCurve.x25519);

            Handshake handshake = new Handshake();
            handshake.MessageType = HandshakeType.ClientHello;
            handshake.Length = (uint)clientHello.CalculateSize();
            handshake.MessageSequence = 0;
            handshake.FragmentOffset = 0;
            handshake.FragmentLength = handshake.Length;

            // Describe the record
            int plaintextLength = (int)(Handshake.Size + handshake.Length);
            Record outgoingRecord = new Record();
            outgoingRecord.ContentType = ContentType.Handshake;
            outgoingRecord.Epoch = this.epoch;
            outgoingRecord.SequenceNumber = this.currentEpoch.NextOutgoingSequence;
            outgoingRecord.Length = (ushort)this.currentEpoch.RecordProtection.GetEncryptedSize(plaintextLength);
            ++this.currentEpoch.NextOutgoingSequence;

            // Convert the record to wire format
            ByteSpan packet = new byte[Record.Size + outgoingRecord.Length];
            ByteSpan writer = packet;
            outgoingRecord.Encode(packet);
            writer = writer.Slice(Record.Size);
            handshake.Encode(writer);
            writer = writer.Slice(Handshake.Size);
            clientHello.Encode(writer);

            // Write ClientHello to the verification stream
            this.nextEpoch.VerificationStream.AddData(
                packet.Slice(
                      Record.Size
                    , Handshake.Size + (int)handshake.Length
                )
            );

            // Protect the record
            this.currentEpoch.RecordProtection.EncryptClientPlaintext(
                  packet.Slice(Record.Size, outgoingRecord.Length)
                , packet.Slice(Record.Size, plaintextLength)
                , ref outgoingRecord
            );

            this.nextEpoch.State = HandshakeState.ExpectingServerHello;
            if (this.nextEpoch.NegotiationStartTime == DateTime.MinValue) this.nextEpoch.NegotiationStartTime = DateTime.UtcNow;
            this.nextEpoch.NextPacketResendTime = DateTime.UtcNow + this.handshakeResendTimeout;
            base.WriteBytesToConnection(packet.GetUnderlyingArray(), packet.Length);
        }

        protected void Test_SendClientHello(Func<ClientHello, ByteSpan, ByteSpan> encodeCallback)
        {
            // Reset our verification stream
            this.nextEpoch.VerificationStream.Reset();

            // Describe our ClientHello flight
            ClientHello clientHello = new ClientHello();
            clientHello.Random = this.nextEpoch.ClientRandom;
            clientHello.Cookie = this.nextEpoch.Cookie;
            clientHello.CipherSuites = new byte[2];
            clientHello.CipherSuites.WriteBigEndian16((ushort)CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256);
            clientHello.SupportedCurves = new byte[2];
            clientHello.SupportedCurves.WriteBigEndian16((ushort)NamedCurve.x25519);

            Handshake handshake = new Handshake();
            handshake.MessageType = HandshakeType.ClientHello;
            handshake.Length = (uint)clientHello.CalculateSize();
            handshake.MessageSequence = 0;
            handshake.FragmentOffset = 0;
            handshake.FragmentLength = handshake.Length;

            // Describe the record
            int plaintextLength = (int)(Handshake.Size + handshake.Length);
            Record outgoingRecord = new Record();
            outgoingRecord.ContentType = ContentType.Handshake;
            outgoingRecord.Epoch = this.epoch;
            outgoingRecord.SequenceNumber = this.currentEpoch.NextOutgoingSequence;
            outgoingRecord.Length = (ushort)this.currentEpoch.RecordProtection.GetEncryptedSize(plaintextLength);
            ++this.currentEpoch.NextOutgoingSequence;

            // Convert the record to wire format
            ByteSpan packet = new byte[Record.Size + outgoingRecord.Length];
            ByteSpan writer = packet;
            outgoingRecord.Encode(packet);
            writer = writer.Slice(Record.Size);
            handshake.Encode(writer);
            writer = writer.Slice(Handshake.Size);

            writer = encodeCallback(clientHello, writer);

            // Write ClientHello to the verification stream
            this.nextEpoch.VerificationStream.AddData(
                packet.Slice(
                      Record.Size
                    , Handshake.Size + (int)handshake.Length
                )
            );

            // Protect the record
            this.currentEpoch.RecordProtection.EncryptClientPlaintext(
                  packet.Slice(Record.Size, outgoingRecord.Length)
                , packet.Slice(Record.Size, plaintextLength)
                , ref outgoingRecord
            );

            this.nextEpoch.State = HandshakeState.ExpectingServerHello;
            if (this.nextEpoch.NegotiationStartTime == DateTime.MinValue) this.nextEpoch.NegotiationStartTime = DateTime.UtcNow;
            this.nextEpoch.NextPacketResendTime = DateTime.UtcNow + this.handshakeResendTimeout;
            base.WriteBytesToConnection(packet.GetUnderlyingArray(), packet.Length);
        }

        /// <summary>
        /// Send (resend) the ClientKeyExchange flight
        /// </summary>
        /// <param name="isRetransmit">
        /// True if this is a retransmit of the flight. Otherwise,
        /// false
        /// </param>
        private void SendClientKeyExchangeFlight(bool isRetransmit)
        {
            // Describe our flight
            Handshake keyExchangeHandshake = new Handshake();
            keyExchangeHandshake.MessageType = HandshakeType.ClientKeyExchange;
            keyExchangeHandshake.Length = (ushort)this.nextEpoch.Handshake.CalculateClientMessageSize();
            keyExchangeHandshake.MessageSequence = 5;
            keyExchangeHandshake.FragmentOffset = 0;
            keyExchangeHandshake.FragmentLength = keyExchangeHandshake.Length;

            Record keyExchangeRecord = new Record();
            keyExchangeRecord.ContentType = ContentType.Handshake;
            keyExchangeRecord.Epoch = this.epoch;
            keyExchangeRecord.SequenceNumber = this.currentEpoch.NextOutgoingSequence;
            keyExchangeRecord.Length = (ushort)this.currentEpoch.RecordProtection.GetEncryptedSize(Handshake.Size + (int)keyExchangeHandshake.Length);
            ++this.currentEpoch.NextOutgoingSequence;

            Record changeCipherSpecRecord = new Record();
            changeCipherSpecRecord.ContentType = ContentType.ChangeCipherSpec;
            changeCipherSpecRecord.Epoch = this.epoch;
            changeCipherSpecRecord.SequenceNumber = this.currentEpoch.NextOutgoingSequence;
            changeCipherSpecRecord.Length = (ushort)this.currentEpoch.RecordProtection.GetEncryptedSize(ChangeCipherSpec.Size);
            ++this.currentEpoch.NextOutgoingSequence;

            Handshake finishedHandshake = new Handshake();
            finishedHandshake.MessageType = HandshakeType.Finished;
            finishedHandshake.Length = Finished.Size;
            finishedHandshake.MessageSequence = 6;
            finishedHandshake.FragmentOffset = 0;
            finishedHandshake.FragmentLength = finishedHandshake.Length;

            Record finishedRecord = new Record();
            finishedRecord.ContentType = ContentType.Handshake;
            finishedRecord.Epoch = this.nextEpoch.Epoch;
            finishedRecord.SequenceNumber = this.nextEpoch.NextOutgoingSequence;
            finishedRecord.Length = (ushort)this.nextEpoch.RecordProtection.GetEncryptedSize(Handshake.Size + (int)finishedHandshake.Length);
            ++this.nextEpoch.NextOutgoingSequence;

            // Encode flight to wire format
            int packetLength = 0
                + Record.Size + keyExchangeRecord.Length
                + Record.Size + changeCipherSpecRecord.Length
                + Record.Size + finishedRecord.Length;
                ;
            ByteSpan packet = new byte[packetLength];
            ByteSpan writer = packet;

            keyExchangeRecord.Encode(writer);
            writer = writer.Slice(Record.Size);
            keyExchangeHandshake.Encode(writer);
            writer = writer.Slice(Handshake.Size);
            this.nextEpoch.Handshake.EncodeClientKeyExchangeMessage(writer);

            ByteSpan startOfChangeCipherSpecRecord = packet.Slice(Record.Size + keyExchangeRecord.Length);
            writer = startOfChangeCipherSpecRecord;
            changeCipherSpecRecord.Encode(writer);
            writer = writer.Slice(Record.Size);
            ChangeCipherSpec.Encode(writer);
            writer = writer.Slice(ChangeCipherSpec.Size);

            ByteSpan startOfFinishedRecord = startOfChangeCipherSpecRecord.Slice(Record.Size + changeCipherSpecRecord.Length);
            writer = startOfFinishedRecord;
            finishedRecord.Encode(writer);
            writer = writer.Slice(Record.Size);
            finishedHandshake.Encode(writer);
            writer = writer.Slice(Handshake.Size);

            // Interject here to writer our client key exchange
            // message into the verification stream
            if (!isRetransmit)
            {
                this.nextEpoch.VerificationStream.AddData(
                    packet.Slice(
                          Record.Size
                        , Handshake.Size + (int)keyExchangeHandshake.Length
                    )
                );
            }

            // Calculate the hash of the verification stream
            ByteSpan handshakeHash = new byte[Sha256Stream.DigestSize];
            this.nextEpoch.VerificationStream.CopyOrCalculateFinalHash(handshakeHash);

            // Expand our master secret into Finished digests for the client and server
            PrfSha256.ExpandSecret(
                  this.nextEpoch.ServerVerification
                , this.nextEpoch.MasterSecret
                , PrfLabel.SERVER_FINISHED
                , handshakeHash
            );

            PrfSha256.ExpandSecret(
                  writer.Slice(0, Finished.Size)
                , this.nextEpoch.MasterSecret
                , PrfLabel.CLIENT_FINISHED
                , handshakeHash
            );
            writer = writer.Slice(Finished.Size);

            // Protect the ClientKeyExchange record
            this.currentEpoch.RecordProtection.EncryptClientPlaintext(
                  packet.Slice(Record.Size, keyExchangeRecord.Length)
                , packet.Slice(Record.Size, Handshake.Size + (int)keyExchangeHandshake.Length)
                , ref keyExchangeRecord
            );

            // Protect the ChangeCipherSpec record
            this.currentEpoch.RecordProtection.EncryptClientPlaintext(
                  startOfChangeCipherSpecRecord.Slice(Record.Size, changeCipherSpecRecord.Length)
                , startOfChangeCipherSpecRecord.Slice(Record.Size, ChangeCipherSpec.Size)
                , ref changeCipherSpecRecord
            );

            // Protect the Finished record
            this.nextEpoch.RecordProtection.EncryptClientPlaintext(
                  startOfFinishedRecord.Slice(Record.Size, finishedRecord.Length)
                , startOfFinishedRecord.Slice(Record.Size, Handshake.Size + (int)finishedHandshake.Length)
                , ref finishedRecord
            );

            this.nextEpoch.State = HandshakeState.ExpectingChangeCipherSpec;
            this.nextEpoch.NextPacketResendTime = DateTime.UtcNow + this.handshakeResendTimeout;
#if DEBUG
            if (DropClientKeyExchangeFlight())
            {
                return;
            }
#endif
            base.WriteBytesToConnection(packet.GetUnderlyingArray(), packet.Length);
        }

        protected virtual bool DropClientKeyExchangeFlight()
        {
            return false;
        }
    }
}
