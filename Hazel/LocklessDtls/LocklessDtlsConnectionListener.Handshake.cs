using Hazel.Crypto;
using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hazel.Dtls
{
    /// <summary>
    /// Listens for new UDP-DTLS connections and creates UdpConnections for them.
    /// </summary>
    /// <inheritdoc />
    public partial class LocklessDtlsConnectionListener
    {
        private const int MaxCertFragmentSizeV0 = 1200;

        // Min MTU - UDP+IP header - 1 (for good measure. :))
        private const int MaxCertFragmentSizeV1 = 576 - 32 - 1;

        private RandomNumberGenerator random;

        // Private key component of certificate's public key
        private ByteSpan encodedCertificate;
        private RSA certificatePrivateKey;

        // HMAC key to validate ClientHello cookie
        private ThreadedHmacHelper hmacHelper;
        private HMAC CurrentCookieHmac => hmacHelper.GetCurrentCookieHmacsForThread();
        private HMAC PreviousCookieHmac => hmacHelper.GetPreviousCookieHmacsForThread();

        /// <summary>
        /// Set the certificate key pair for the listener
        /// </summary>
        /// <param name="certificate">Certificate for the server</param>
        public void SetCertificate(X509Certificate2 certificate)
        {
            if (!certificate.HasPrivateKey)
            {
                throw new ArgumentException("Certificate must have a private key attached", nameof(certificate));
            }

            RSA privateKey = certificate.GetRSAPrivateKey();
            if (privateKey == null)
            {
                throw new ArgumentException("Certificate must be signed by an RSA key", nameof(certificate));
            }

            this.certificatePrivateKey?.Dispose();
            this.certificatePrivateKey = privateKey;

            this.encodedCertificate = Certificate.Encode(certificate);
        }

        /// <summary>
        /// Handle an incoming datagram from the network
        /// </summary>
        private void ProcessIncomingMessage(ByteSpan message, LocklessDtlsServerConnection connection)
        {
            PeerData peer = connection.PeerData;
            IPEndPoint peerAddress = connection.EndPoint;
            if (peer == null)
            {
                HandleNonPeerRecord(message, connection);
                return;
            }

            // Each incoming packet may contain multiple DTLS
            // records
            while (message.Length > 0)
            {
                Record record;
                if (!Record.Parse(out record, peer.ProtocolVersion, message))
                {
                    this.Logger.WriteError($"Dropping malformed record from `{peerAddress}`");
                    return;
                }
                message = message.Slice(Record.Size);

                if (message.Length < record.Length)
                {
                    this.Logger.WriteError($"Dropping malformed record from `{peerAddress}` Length({record.Length}) AvailableBytes({message.Length})");
                    return;
                }

                ByteSpan recordPayload = message.Slice(0, record.Length);
                message = message.Slice(record.Length);

                // Early-out and drop ApplicationData records
                if (record.ContentType == ContentType.ApplicationData && !peer.CanHandleApplicationData)
                {
                    this.Logger.WriteInfo($"Dropping ApplicationData record from `{peerAddress}` Cannot process yet");
                    continue;
                }

                // Drop records from a different epoch
                if (record.Epoch != peer.Epoch)
                {
                    // Handle existing client negotiating a new connection
                    if (record.Epoch == 0 && record.ContentType == ContentType.Handshake)
                    {
                        ByteSpan handshakePayload = recordPayload;

                        Handshake handshake;
                        if (!Handshake.Parse(out handshake, recordPayload))
                        {
                            this.Logger.WriteError($"Dropping malformed re-negotiation Handshake from `{peerAddress}`");
                            continue;
                        }
                        handshakePayload = handshakePayload.Slice(Handshake.Size);

                        if (handshake.FragmentOffset != 0 || handshake.Length != handshake.FragmentLength)
                        {
                            this.Logger.WriteError($"Dropping fragmented re-negotiation Handshake from `{peerAddress}`");
                            continue;
                        }
                        else if (handshake.MessageType != HandshakeType.ClientHello)
                        {
                            this.Logger.WriteVerbose($"Dropping non-ClientHello re-negotiation Handshake from `{peerAddress}`");
                            continue;
                        }
                        else if (handshakePayload.Length < handshake.Length)
                        {
                            this.Logger.WriteError($"Dropping malformed re-negotiation Handshake from `{peerAddress}`: Length({handshake.Length}) AvailableBytes({handshakePayload.Length})");
                        }

                        if (!this.HandleClientHello(peer, peerAddress, ref record, ref handshake, recordPayload, handshakePayload))
                        {
                            return;
                        }
                        continue;
                    }

                    this.Logger.WriteVerbose($"Dropping bad-epoch record from `{peerAddress}` RecordEpoch({record.Epoch}) CurrentEpoch({peer.Epoch})");
                    continue;
                }

                // Prevent replay attacks by dropping records
                // we've already processed
                int windowIndex = (int)(peer.CurrentEpoch.NextExpectedSequence - record.SequenceNumber - 1);
                ulong windowMask = 1ul << windowIndex;
                if (record.SequenceNumber < peer.CurrentEpoch.NextExpectedSequence)
                {
                    if (windowIndex >= 64)
                    {
                        this.Logger.WriteInfo($"Dropping too-old record from `{peerAddress}` Sequence({record.SequenceNumber}) Expected({peer.CurrentEpoch.NextExpectedSequence})");
                        continue;
                    }

                    if ((peer.CurrentEpoch.PreviousSequenceWindowBitmask & windowMask) != 0)
                    {
                        this.Logger.WriteInfo($"Dropping duplicate record from `{peerAddress}`");
                        continue;
                    }
                }

                // Validate record authenticity
                int decryptedSize = peer.CurrentEpoch.RecordProtection.GetDecryptedSize(recordPayload.Length);
                if (decryptedSize < 0)
                {
                    this.Logger.WriteInfo($"Dropping malformed record: Length {recordPayload.Length} Decrypted length: {decryptedSize}");
                    continue;
                }

                ByteSpan decryptedPayload = recordPayload.ReuseSpanIfPossible(decryptedSize);
                ProtocolVersion protocolVersion = peer.ProtocolVersion;

                if (!peer.CurrentEpoch.RecordProtection.DecryptCiphertextFromClient(decryptedPayload, recordPayload, ref record))
                {
                    this.Logger.WriteVerbose($"Dropping non-authentic {record.ContentType} record from `{peerAddress}`");
                    return;
                }

                recordPayload = decryptedPayload;

                // Update our squence number bookeeping
                if (record.SequenceNumber >= peer.CurrentEpoch.NextExpectedSequence)
                {
                    int windowShift = (int)(record.SequenceNumber + 1 - peer.CurrentEpoch.NextExpectedSequence);
                    peer.CurrentEpoch.PreviousSequenceWindowBitmask <<= windowShift;
                    peer.CurrentEpoch.NextExpectedSequence = record.SequenceNumber + 1;
                }
                else
                {
                    peer.CurrentEpoch.PreviousSequenceWindowBitmask |= windowMask;
                }

                // This is handy for debugging, but too verbose even for verbose.
                // this.Logger.WriteVerbose($"Record type {record.ContentType} ({peer.NextEpoch.State})");
                switch (record.ContentType)
                {
                    case ContentType.ChangeCipherSpec:
                        if (peer.NextEpoch.State != HandshakeState.ExpectingChangeCipherSpec)
                        {
                            this.Logger.WriteError($"Dropping unexpected ChangeChiperSpec record from `{peerAddress}` State({peer.NextEpoch.State})");
                            break;
                        }
                        else if (peer.NextEpoch.RecordProtection == null)
                        {
                            ///NOTE(mendsley): This _should_ not
                            /// happen on a well-formed server.
                            Debug.Assert(false, "How did we receive a ChangeCipherSpec message without a pending record protection instance?");

                            this.Logger.WriteError($"Dropping ChangeCipherSpec message from `{peerAddress}`: No pending record protection");
                            break;
                        }

                        if (!ChangeCipherSpec.Parse(recordPayload))
                        {
                            this.Logger.WriteError($"Dropping malformed ChangeCipherSpec message from `{peerAddress}`");
                            break;
                        }

                        // Migrate to the next epoch
                        peer.Epoch = peer.NextEpoch.Epoch;
                        peer.CanHandleApplicationData = false; // Need a Finished message
                        peer.CurrentEpoch.NextOutgoingSequenceForPreviousEpoch = peer.CurrentEpoch.NextOutgoingSequence;
                        peer.CurrentEpoch.PreviousRecordProtection?.Dispose();
                        peer.CurrentEpoch.PreviousRecordProtection = peer.CurrentEpoch.RecordProtection;
                        peer.CurrentEpoch.RecordProtection = peer.NextEpoch.RecordProtection;
                        peer.CurrentEpoch.NextOutgoingSequence = 1;
                        peer.CurrentEpoch.NextExpectedSequence = 1;
                        peer.CurrentEpoch.PreviousSequenceWindowBitmask = 0;
                        peer.NextEpoch.ClientVerification.CopyTo(peer.CurrentEpoch.ExpectedClientFinishedVerification);
                        peer.NextEpoch.ServerVerification.CopyTo(peer.CurrentEpoch.ServerFinishedVerification);

                        peer.NextEpoch.State = HandshakeState.ExpectingHello;
                        peer.NextEpoch.Handshake?.Dispose();
                        peer.NextEpoch.Handshake = null;
                        peer.NextEpoch.NextOutgoingSequence = 1;
                        peer.NextEpoch.RecordProtection = null;
                        peer.NextEpoch.VerificationStream.Reset();
                        peer.NextEpoch.ClientVerification.SecureClear();
                        peer.NextEpoch.ServerVerification.SecureClear();
                        break;

                    case ContentType.Alert:
                        this.Logger.WriteError($"Dropping unsupported Alert record from `{peerAddress}`");
                        break;

                    case ContentType.Handshake:
                        if (!ProcessHandshake(connection, ref record, recordPayload))
                        {
                            return;
                        }
                        break;

                    case ContentType.ApplicationData:
                        // Forward data to the application
                        MessageReader reader = MessageReader.GetSized(recordPayload.Length);
                        reader.Length = recordPayload.Length;
                        recordPayload.CopyTo(reader.Buffer);

                        HandleApplicationData(reader, connection);
                        break;
                }
            }
        }

        /// <summary>
        /// Process an incoming Handshake protocol message
        /// </summary>
        /// <param name="peer">Originating peer</param>
        /// <param name="peerAddress">Peer's network address</param>
        /// <param name="record">Parent record</param>
        /// <param name="message">Record payload</param>
        /// <returns>
        /// True if further processing of the underlying datagram
        /// should be continues. Otherwise, false.
        /// </returns>
        private bool ProcessHandshake(LocklessDtlsServerConnection connection, ref Record record, ByteSpan message)
        {
            PeerData peer = connection.PeerData;
            IPEndPoint peerAddress = connection.EndPoint;

            // Each record may have multiple handshake payloads
            while (message.Length > 0)
            {
                ByteSpan originalMessage = message;

                Handshake handshake;
                if (!Handshake.Parse(out handshake, message))
                {
                    this.Logger.WriteError($"Dropping malformed Handshake message from `{peerAddress}`");
                    return false;
                }
                message = message.Slice(Handshake.Size);

                if (message.Length < handshake.Length)
                {
                    this.Logger.WriteError($"Dropping malformed Handshake message from `{peerAddress}`");
                    return false;
                }

                ByteSpan payload = message.Slice(0, (int)message.Length);
                message = message.Slice((int)handshake.Length);
                originalMessage = originalMessage.Slice(0, Handshake.Size + (int)handshake.Length);

                // We do not support fragmented handshake messages
                // from the client
                if (handshake.FragmentOffset != 0 || handshake.FragmentLength != handshake.Length)
                {
                    this.Logger.WriteError($"Dropping fragmented Handshake message from `{peerAddress}` Offset({handshake.FragmentOffset}) FragmentLength({handshake.FragmentLength}) Length({handshake.Length})");
                    continue;
                }

                ByteSpan packet;
                ByteSpan writer;

#if DEBUG
                this.Logger.WriteVerbose($"Received handshake {handshake.MessageType} ({peer.NextEpoch.State})");
#endif
                switch (handshake.MessageType)
                {
                    case HandshakeType.ClientHello:
                        if (!this.HandleClientHello(peer, peerAddress, ref record, ref handshake, originalMessage, payload))
                        {
                            return false;
                        }
                        break;

                    case HandshakeType.ClientKeyExchange:
                        if (peer.NextEpoch.State != HandshakeState.ExpectingClientKeyExchange)
                        {
                            this.Logger.WriteError($"Dropping unexpected ClientKeyExchange message form `{peerAddress}` State({peer.NextEpoch.State})");
                            continue;
                        }
                        else if (handshake.MessageSequence != 5)
                        {
                            this.Logger.WriteError($"Dropping bad-sequence ClientKeyExchange message from `{peerAddress}` MessageSequence({handshake.MessageSequence})");
                            continue;
                        }

                        ByteSpan sharedSecret = new byte[peer.NextEpoch.Handshake.SharedKeySize()];
                        if (!peer.NextEpoch.Handshake.VerifyClientMessageAndGenerateSharedKey(sharedSecret, payload))
                        {
                            this.Logger.WriteError($"Dropping malformed ClientKeyExchange message from `{peerAddress}`");
                            return false;
                        }

                        // Record incoming ClientKeyExchange message
                        // to verification stream
                        peer.NextEpoch.VerificationStream.AddData(originalMessage);

                        ByteSpan randomSeed = new byte[2 * Random.Size];
                        peer.NextEpoch.ClientRandom.CopyTo(randomSeed);
                        peer.NextEpoch.ServerRandom.CopyTo(randomSeed.Slice(Random.Size));

                        const int MasterSecretSize = 48;
                        ByteSpan masterSecret = new byte[MasterSecretSize];
                        PrfSha256.ExpandSecret(
                            masterSecret
                            , sharedSecret
                            , PrfLabel.MASTER_SECRET
                            , randomSeed
                        );

                        // Create the record protection for the upcoming epoch
                        switch (peer.NextEpoch.SelectedCipherSuite)
                        {
                            case CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256:
                                peer.NextEpoch.RecordProtection = new Aes128GcmRecordProtection(
                                    masterSecret
                                    , peer.NextEpoch.ServerRandom
                                    , peer.NextEpoch.ClientRandom);
                                break;

                            default:
                                Debug.Assert(false, $"How did we agree to a cipher suite {peer.NextEpoch.SelectedCipherSuite} we can't create?");
                                this.Logger.WriteError($"Dropping ClientKeyExchange message from `{peerAddress}` Unsuppored cipher suite");
                                return false;
                        }

                        // Generate verification signatures
                        ByteSpan handshakeStreamHash = new byte[Sha256Stream.DigestSize];
                        peer.NextEpoch.VerificationStream.CopyOrCalculateFinalHash(handshakeStreamHash);

                        PrfSha256.ExpandSecret(
                            peer.NextEpoch.ClientVerification
                            , masterSecret
                            , PrfLabel.CLIENT_FINISHED
                            , handshakeStreamHash
                        );
                        PrfSha256.ExpandSecret(
                            peer.NextEpoch.ServerVerification
                            , masterSecret
                            , PrfLabel.SERVER_FINISHED
                            , handshakeStreamHash
                        );


                        // Update handshake state
                        masterSecret.SecureClear();
                        peer.NextEpoch.State = HandshakeState.ExpectingChangeCipherSpec;
                        break;

                    case HandshakeType.Finished:
                        // Unlike other handshake messages, this is
                        // for the current epoch - not the next epoch

                        // Cannot process a Finished message for
                        // epoch 0
                        if (peer.Epoch == 0)
                        {
                            this.Logger.WriteError($"Dropping Finished message for 0-epoch from `{peerAddress}`");
                            continue;
                        }
                        // Cannot process a Finished message when we
                        // are negotiating the next epoch
                        else if (peer.NextEpoch.State != HandshakeState.ExpectingHello)
                        {
                            this.Logger.WriteError($"Dropping Finished message while negotiating new epoch from `{peerAddress}`");
                            continue;
                        }
                        // Cannot process a Finished message without
                        // verify data
                        else if (peer.CurrentEpoch.ExpectedClientFinishedVerification.Length != Finished.Size || peer.CurrentEpoch.ServerFinishedVerification.Length != Finished.Size)
                        {
                            ///NOTE(mendsley): This _should_ not
                            /// happen on a well-formed server.
                            Debug.Assert(false, "How do we have an established non-zero epoch without verify data?");

                            this.Logger.WriteError($"Dropping Finished message (no verify data) from `{peerAddress}`");
                            return false;
                        }
                        // Cannot process a Finished message without
                        // record protection for the previous epoch
                        else if (peer.CurrentEpoch.PreviousRecordProtection == null)
                        {
                            ///NOTE(mendsley): This _should_ not
                            /// happen on a well-formed server.
                            Debug.Assert(false, "How do we have an established non-zero epoch with record protection for the previous epoch?");

                            this.Logger.WriteError($"Dropping Finished message from `{peerAddress}`: No previous epoch record protection");
                            return false;
                        }

                        // Verify message sequence
                        if (handshake.MessageSequence != 6)
                        {
                            this.Logger.WriteError($"Dropping bad-sequence Finished message from `{peerAddress}` MessageSequence({handshake.MessageSequence})");
                            continue;
                        }

                        // Verify the client has the correct
                        // handshake sequence
                        if (payload.Length != Finished.Size)
                        {
                            this.Logger.WriteError($"Dropping malformed Finished message from `{peerAddress}`");
                            return false;
                        }
                        else if (1 != Crypto.Const.ConstantCompareSpans(payload, peer.CurrentEpoch.ExpectedClientFinishedVerification))
                        {

#if DEBUG
                            this.Logger.WriteError($"Dropping non-verified Finished Handshake from `{peerAddress}`");
#endif

                            // Abort the connection here
                            //
                            // The client is either broken, or
                            // doen not agree on our epoch settings.
                            //
                            // Either way, there is not a feasible
                            // way to progress the connection.
                            this.RemoveConnectionTo(connection.EndPoint);
                            connection.Dispose();
                            return false;
                        }

                        ProtocolVersion protocolVersion = peer.ProtocolVersion;

                        // Describe our ChangeCipherSpec+Finished
                        Handshake outgoingHandshake = new Handshake();
                        outgoingHandshake.MessageType = HandshakeType.Finished;
                        outgoingHandshake.Length = Finished.Size;
                        outgoingHandshake.MessageSequence = 7;
                        outgoingHandshake.FragmentOffset = 0;
                        outgoingHandshake.FragmentLength = outgoingHandshake.Length;

                        Record changeCipherSpecRecord = new Record();
                        changeCipherSpecRecord.ContentType = ContentType.ChangeCipherSpec;
                        changeCipherSpecRecord.ProtocolVersion = protocolVersion;
                        changeCipherSpecRecord.Epoch = (ushort)(peer.Epoch - 1);
                        changeCipherSpecRecord.SequenceNumber = peer.CurrentEpoch.NextOutgoingSequenceForPreviousEpoch;
                        changeCipherSpecRecord.Length = (ushort)peer.CurrentEpoch.PreviousRecordProtection.GetEncryptedSize(ChangeCipherSpec.Size);
                        ++peer.CurrentEpoch.NextOutgoingSequenceForPreviousEpoch;

                        int plaintextFinishedPayloadSize = Handshake.Size + (int)outgoingHandshake.Length;
                        Record finishedRecord = new Record();
                        finishedRecord.ContentType = ContentType.Handshake;
                        finishedRecord.ProtocolVersion = protocolVersion;
                        finishedRecord.Epoch = peer.Epoch;
                        finishedRecord.SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence;
                        finishedRecord.Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(plaintextFinishedPayloadSize);
                        ++peer.CurrentEpoch.NextOutgoingSequence;

                        // Encode the flight into wire format
                        packet = new byte[Record.Size + changeCipherSpecRecord.Length + Record.Size + finishedRecord.Length];
                        writer = packet;
                        changeCipherSpecRecord.Encode(writer);
                        writer = writer.Slice(Record.Size);
                        ChangeCipherSpec.Encode(writer);

                        ByteSpan startOfFinishedRecord = packet.Slice(Record.Size + changeCipherSpecRecord.Length);
                        writer = startOfFinishedRecord;
                        finishedRecord.Encode(writer);
                        writer = writer.Slice(Record.Size);
                        outgoingHandshake.Encode(writer);
                        writer = writer.Slice(Handshake.Size);
                        peer.CurrentEpoch.ServerFinishedVerification.CopyTo(writer);

                        // Protect the ChangeChipherSpec record
                        peer.CurrentEpoch.PreviousRecordProtection.EncryptServerPlaintext(
                            packet.Slice(Record.Size, changeCipherSpecRecord.Length),
                            packet.Slice(Record.Size, ChangeCipherSpec.Size),
                            ref changeCipherSpecRecord
                        );

                        // Protect the Finished Handshake record
                        peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
                            startOfFinishedRecord.Slice(Record.Size, finishedRecord.Length),
                            startOfFinishedRecord.Slice(Record.Size, plaintextFinishedPayloadSize),
                            ref finishedRecord
                        );

                        // Current epoch can now handle application data
                        peer.CanHandleApplicationData = true;

                        this.sendQueue.TryAdd(new SendMessageInfo(packet, peerAddress));
                        break;

                    // Drop messages that we do not support
                    case HandshakeType.CertificateVerify:
                        this.Logger.WriteError($"Dropping unsupported Handshake message from `{peerAddress}` MessageType({handshake.MessageType})");
                        continue;

                    // Drop messages that originate from the server
                    case HandshakeType.HelloRequest:
                    case HandshakeType.ServerHello:
                    case HandshakeType.HelloVerifyRequest:
                    case HandshakeType.Certificate:
                    case HandshakeType.ServerKeyExchange:
                    case HandshakeType.CertificateRequest:
                    case HandshakeType.ServerHelloDone:
                        this.Logger.WriteError($"Dropping server Handshake message from `{peerAddress}` MessageType({handshake.MessageType})");
                        continue;
                }
            }

            return true;
        }

        /// <summary>
        /// Handle a ClientHello message for a peer
        /// </summary>
        /// <param name="peer">Originating peer</param>
        /// <param name="peerAddress">Peer address</param>
        /// <param name="record">Parent record</param>
        /// <param name="handshake">Parent Handshake header</param>
        /// <param name="payload">Handshake payload</param>
        private bool HandleClientHello(PeerData peer, IPEndPoint peerAddress, ref Record record, ref Handshake handshake, ByteSpan originalMessage, ByteSpan payload)
        {
            // Verify message sequence
            if (handshake.MessageSequence != 0)
            {
                this.Logger.WriteError($"Dropping bad-sequence ClientHello from `{peerAddress}` MessageSequence({handshake.MessageSequence})`");
                return true;
            }

            // Make sure we can handle a ClientHello message
            if (peer.NextEpoch.State != HandshakeState.ExpectingHello && peer.NextEpoch.State != HandshakeState.ExpectingClientKeyExchange)
            {
                // Always handle ClientHello for epoch 0
                if (record.Epoch != 0)
                {
                    this.Logger.WriteError($"Dropping ClientHello from `{peer}` Not expecting ClientHello");
                    return true;
                }
            }

            ProtocolVersion protocolVersion = peer.ProtocolVersion;
            if (!ClientHello.Parse(out ClientHello clientHello, protocolVersion, payload))
            {
                this.Logger.WriteError($"Dropping malformed ClientHello Handshake message from `{peerAddress}`");
                return false;
            }

            // Find an acceptable cipher suite we can use
            CipherSuite selectedCipherSuite = CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256;
            if (!clientHello.ContainsCipherSuite(CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256) || !clientHello.ContainsCurve(NamedCurve.x25519))
            {
                this.Logger.WriteError($"Dropping ClientHello from `{peerAddress}` No compatible cipher suite");
                return false;
            }

            // If this message was not signed by us,
            // request a signed message before doing anything else
            if (!HelloVerifyRequest.VerifyCookie(clientHello.Cookie, peerAddress, this.CurrentCookieHmac))
            {
                if (!HelloVerifyRequest.VerifyCookie(clientHello.Cookie, peerAddress, this.PreviousCookieHmac))
                {
                    ulong outgoingSequence = 1;
                    IRecordProtection recordProtection = NullRecordProtection.Instance;
                    if (record.Epoch != 0)
                    {
                        outgoingSequence = peer.CurrentEpoch.NextExpectedSequence;
                        ++peer.CurrentEpoch.NextOutgoingSequenceForPreviousEpoch;

                        recordProtection = peer.CurrentEpoch.RecordProtection;
                    }

#if DEBUG
                    this.Logger.WriteError($"Sending HelloVerifyRequest to peer `{peerAddress}`");
#endif
                    this.SendHelloVerifyRequest(peerAddress, outgoingSequence, record.Epoch, recordProtection, protocolVersion);
                    return true;
                }
            }

            // Client is initiating a brand new connection. We need
            // to destroy the existing connection and establish a
            // new session.
            if (record.Epoch == 0 && peer.Epoch != 0)
            {
                peer.ResetPeer(record.SequenceNumber + 1);
            }

            // Determine if this is an original message, or a retransmission
            bool recordMessagesForVerifyData = false;
            if (peer.NextEpoch.State == HandshakeState.ExpectingHello)
            {
                // Create our handhake cipher suite
                IHandshakeCipherSuite handshakeCipherSuite = null;
                switch (selectedCipherSuite)
                {
                    case CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256:
                        if (clientHello.ContainsCurve(NamedCurve.x25519))
                        {
                            handshakeCipherSuite = new X25519EcdheRsaSha256(this.random);
                        }
                        else
                        {
                            this.Logger.WriteError($"Dropping ClientHello from `{peerAddress}` Could not create TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 cipher suite");
                            return false;
                        }

                        break;

                    default:
                        this.Logger.WriteError($"Dropping ClientHello from `{peerAddress}` Could not create handshake cipher suite");
                        return false;
                }

                peer.Session = clientHello.Session;

                // Update the state of our epoch transition
                peer.NextEpoch.Epoch = (ushort)(record.Epoch + 1);
                peer.NextEpoch.State = HandshakeState.ExpectingClientKeyExchange;
                peer.NextEpoch.SelectedCipherSuite = selectedCipherSuite;
                peer.NextEpoch.Handshake = handshakeCipherSuite;
                clientHello.Random.CopyTo(peer.NextEpoch.ClientRandom);
                peer.NextEpoch.ServerRandom.FillWithRandom(this.random);
                recordMessagesForVerifyData = true;

#if DEBUG
                this.Logger.WriteVerbose($"ClientRandom: {peer.NextEpoch.ClientRandom} ServerRandom: {peer.NextEpoch.ServerRandom}");
#endif

                // Copy the original ClientHello
                // handshake to our verification stream
                peer.NextEpoch.VerificationStream.AddData(
                    originalMessage.Slice(
                        0
                      , Handshake.Size + (int)handshake.Length
                    )
                );
            }

            // The initial record flight from the server
            // contains the following Handshake messages:
            //    * ServerHello
            //    * Certificate
            //    * ServerKeyExchange
            //    * ServerHelloDone
            //
            // The Certificate message is almost always
            // too large to fit into a single datagram,
            // so it is pre-fragmented
            // (see `SetCertificates`). Therefore, we
            // need to send multiple record packets for
            // this flight.
            //
            // The first record contains the ServerHello
            // handshake message, as well as the first
            // portion of the Certificate message.
            //
            // We then send a record packet until the
            // entire Certificate message has been sent
            // to the client.
            //
            // The final record packet contains the
            // ServerKeyExchange and the ServerHelloDone
            // messages.

            // Describe first record of the flight
            ServerHello serverHello = new ServerHello();
            serverHello.ServerProtocolVersion = protocolVersion;
            serverHello.Random = peer.NextEpoch.ServerRandom;
            serverHello.CipherSuite = selectedCipherSuite;

            Handshake serverHelloHandshake = new Handshake();
            serverHelloHandshake.MessageType = HandshakeType.ServerHello;
            serverHelloHandshake.Length = ServerHello.MinSize;
            serverHelloHandshake.MessageSequence = 1;
            serverHelloHandshake.FragmentOffset = 0;
            serverHelloHandshake.FragmentLength = serverHelloHandshake.Length;

            int maxCertFragmentSize = peer.Session.Version == 0 ? MaxCertFragmentSizeV0 : MaxCertFragmentSizeV1;

            // The first certificate data needs to leave room for
            //  * Record header
            //  * ServerHello header
            //  * ServerHello payload
            //  * Certificate header

            var certificateData = this.encodedCertificate;
            int initialCertPadding = Record.Size + Handshake.Size + serverHello.Size + Handshake.Size;
            int certInitialFragmentSize = Math.Min(certificateData.Length, maxCertFragmentSize - initialCertPadding);

            Handshake certificateHandshake = new Handshake();
            certificateHandshake.MessageType = HandshakeType.Certificate;
            certificateHandshake.Length = (uint)certificateData.Length;
            certificateHandshake.MessageSequence = 2;
            certificateHandshake.FragmentOffset = 0;
            certificateHandshake.FragmentLength = (uint)certInitialFragmentSize;

            int initialRecordPayloadSize = 0
                + Handshake.Size + serverHello.Size
                + Handshake.Size + (int)certificateHandshake.FragmentLength
                ;
            Record initialRecord = new Record();
            initialRecord.ContentType = ContentType.Handshake;
            initialRecord.ProtocolVersion = protocolVersion;
            initialRecord.Epoch = peer.Epoch;
            initialRecord.SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence;
            initialRecord.Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(initialRecordPayloadSize);
            ++peer.CurrentEpoch.NextOutgoingSequence;

            // Convert initial record of the flight to
            // wire format
            ByteSpan packet = new byte[Record.Size + initialRecord.Length];
            ByteSpan writer = packet;
            initialRecord.Encode(writer);
            writer = writer.Slice(Record.Size);
            serverHelloHandshake.Encode(writer);
            writer = writer.Slice(Handshake.Size);
            serverHello.Encode(writer);
            writer = writer.Slice(ServerHello.MinSize);
            certificateHandshake.Encode(writer);
            writer = writer.Slice(Handshake.Size);
            certificateData.Slice(0, certInitialFragmentSize).CopyTo(writer);
            certificateData = certificateData.Slice(certInitialFragmentSize);

            // Protect initial record of the flight
            peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
                packet.Slice(Record.Size, initialRecord.Length),
                packet.Slice(Record.Size, initialRecordPayloadSize),
                ref initialRecord
            );

            this.sendQueue.TryAdd(new SendMessageInfo(packet, peerAddress));

            // Record record payload for verification
            if (recordMessagesForVerifyData)
            {
                Handshake fullCeritficateHandshake = certificateHandshake;
                fullCeritficateHandshake.FragmentLength = fullCeritficateHandshake.Length;

                packet = new byte[Handshake.Size + ServerHello.MinSize + Handshake.Size];
                writer = packet;
                serverHelloHandshake.Encode(writer);
                writer = writer.Slice(Handshake.Size);
                serverHello.Encode(writer);
                writer = writer.Slice(ServerHello.MinSize);
                fullCeritficateHandshake.Encode(writer);
                writer = writer.Slice(Handshake.Size);

                peer.NextEpoch.VerificationStream.AddData(packet);
                peer.NextEpoch.VerificationStream.AddData(this.encodedCertificate);
            }

            // Process additional certificate records
            // Subsequent certificate data needs to leave room for
            //  * Record header
            //  * Certificate header
            const int CertPadding = Record.Size + Handshake.Size;
            while (certificateData.Length > 0)
            {
                int certFragmentSize = Math.Min(certificateData.Length, maxCertFragmentSize - CertPadding);

                certificateHandshake.FragmentOffset += certificateHandshake.FragmentLength;
                certificateHandshake.FragmentLength = (uint)certFragmentSize;

                int additionalRecordPayloadSize = Handshake.Size + (int)certificateHandshake.FragmentLength;
                Record additionalRecord = new Record();
                additionalRecord.ContentType = ContentType.Handshake;
                additionalRecord.ProtocolVersion = protocolVersion;
                additionalRecord.Epoch = peer.Epoch;
                additionalRecord.SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence;
                additionalRecord.Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(additionalRecordPayloadSize);
                ++peer.CurrentEpoch.NextOutgoingSequence;

                // Convert record to wire format
                packet = new byte[Record.Size + additionalRecord.Length];
                writer = packet;
                additionalRecord.Encode(writer);
                writer = writer.Slice(Record.Size);
                certificateHandshake.Encode(writer);
                writer = writer.Slice(Handshake.Size);
                certificateData.Slice(0, certFragmentSize).CopyTo(writer);

                certificateData = certificateData.Slice(certFragmentSize);

                // Protect record
                peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
                    packet.Slice(Record.Size, additionalRecord.Length),
                    packet.Slice(Record.Size, additionalRecordPayloadSize),
                    ref additionalRecord
                );

                this.sendQueue.TryAdd(new SendMessageInfo(packet, peerAddress));
            }

            // Describe final record of the flight
            Handshake serverKeyExchangeHandshake = new Handshake();
            serverKeyExchangeHandshake.MessageType = HandshakeType.ServerKeyExchange;
            serverKeyExchangeHandshake.Length = (uint)peer.NextEpoch.Handshake.CalculateServerMessageSize(this.certificatePrivateKey);
            serverKeyExchangeHandshake.MessageSequence = 3;
            serverKeyExchangeHandshake.FragmentOffset = 0;
            serverKeyExchangeHandshake.FragmentLength = serverKeyExchangeHandshake.Length;

            Handshake serverHelloDoneHandshake = new Handshake();
            serverHelloDoneHandshake.MessageType = HandshakeType.ServerHelloDone;
            serverHelloDoneHandshake.Length = 0;
            serverHelloDoneHandshake.MessageSequence = 4;
            serverHelloDoneHandshake.FragmentOffset = 0;
            serverHelloDoneHandshake.FragmentLength = 0;

            int finalRecordPayloadSize = 0
                + Handshake.Size + (int)serverKeyExchangeHandshake.Length
                + Handshake.Size + (int)serverHelloDoneHandshake.Length
                ;
            Record finalRecord = new Record();
            finalRecord.ContentType = ContentType.Handshake;
            finalRecord.ProtocolVersion = protocolVersion;
            finalRecord.Epoch = peer.Epoch;
            finalRecord.SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence;
            finalRecord.Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(finalRecordPayloadSize);
            ++peer.CurrentEpoch.NextOutgoingSequence;

            // Convert final record of the flight to wire
            // format
            packet = new byte[Record.Size + finalRecord.Length];
            writer = packet;
            finalRecord.Encode(writer);
            writer = writer.Slice(Record.Size);
            serverKeyExchangeHandshake.Encode(writer);
            writer = writer.Slice(Handshake.Size);
            peer.NextEpoch.Handshake.EncodeServerKeyExchangeMessage(writer, this.certificatePrivateKey);
            writer = writer.Slice((int)serverKeyExchangeHandshake.Length);
            serverHelloDoneHandshake.Encode(writer);

            // Record record payload for verification
            if (recordMessagesForVerifyData)
            {
                peer.NextEpoch.VerificationStream.AddData(
                    packet.Slice(
                          packet.Offset + Record.Size
                        , finalRecordPayloadSize
                    )
                );
            }

            // Protect final record of the flight
            peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
                packet.Slice(Record.Size, finalRecord.Length),
                packet.Slice(Record.Size, finalRecordPayloadSize),
                ref finalRecord
            );

            this.sendQueue.TryAdd(new SendMessageInfo(packet, peerAddress));

            return true;
        }

        /// <summary>
        /// Handle an incoming packet that is not tied to an existing peer
        /// </summary>
        /// <param name="message">Incoming datagram</param>
        /// <param name="peerAddress">Originating address</param>
        private void HandleNonPeerRecord(ByteSpan message, LocklessDtlsServerConnection connection)
        {
            IPEndPoint peerAddress = connection.EndPoint;
            Record record;
            if (!Record.Parse(out record, expectedProtocolVersion: null, message))
            {
                this.Logger.WriteError($"Dropping malformed record from non-peer `{peerAddress}`");
                return;
            }
            message = message.Slice(Record.Size);

            // The protocol only supports receiving a single record
            // from a non-peer.
            if (record.Length != message.Length)
            {
                // NOTE(mendsley): This isn't always fatal.
                // However, this is an indication that something
                // fishy is going on. In the best case, there's a
                // bug on the client or in the UDP stack (some
                // stacks don't both to verify the checksum). In the
                // worst case we're dealing with a malicious actor.
                // In the malicious case, we'll end up dropping the
                // connection later in the process.
                if (message.Length < record.Length)
                {
                    this.Logger.WriteInfo($"Dropping bad record from non-peer `{peerAddress}`. Msg length {message.Length} < {record.Length}");
                    return;
                }
            }

            // We only accept zero-epoch records from non-peers
            if (record.Epoch != 0)
            {
                ///NOTE(mendsley): Not logging anything here, as
                /// this could easily be latent data arriving from a
                /// recently disconnected peer.
                return;
            }

            // We only accept Handshake protocol messages from non-peers
            if (record.ContentType != ContentType.Handshake)
            {
                this.Logger.WriteError($"Dropping non-handhsake message from non-peer `{peerAddress}`");
                return;
            }

            ByteSpan originalMessage = message;

            Handshake handshake;
            if (!Handshake.Parse(out handshake, message))
            {
                this.Logger.WriteError($"Dropping malformed handshake message from non-peer `{peerAddress}`");
                return;
            }

            // We only accept ClientHello messages from non-peers
            if (handshake.MessageType != HandshakeType.ClientHello)
            {
#if DEBUG
                this.Logger.WriteError($"Dropping non-ClientHello ({handshake.MessageType}) message from non-peer `{peerAddress}`");
#endif
                return;
            }
            message = message.Slice(Handshake.Size);

            if (!ClientHello.Parse(out ClientHello clientHello, expectedProtocolVersion: null, message))
            {
                this.Logger.WriteError($"Dropping malformed ClientHello message from non-peer `{peerAddress}`");
                return;
            }

            // If this ClientHello is not signed by us, request the
            // client send us a signed message
            if (!HelloVerifyRequest.VerifyCookie(clientHello.Cookie, peerAddress, this.CurrentCookieHmac))
            {
                if (!HelloVerifyRequest.VerifyCookie(clientHello.Cookie, peerAddress, this.PreviousCookieHmac))
                {
#if DEBUG
                    this.Logger.WriteVerbose($"Sending HelloVerifyRequest to non-peer `{peerAddress}`");
#endif
                    this.SendHelloVerifyRequest(peerAddress, 1, 0, NullRecordProtection.Instance, clientHello.ClientProtocolVersion);
                    return;
                }
            }

            // Allocate state for the new peer and register it
            connection.SetPeerData(new PeerData(record.SequenceNumber + 1, clientHello.ClientProtocolVersion));
            this.ProcessHandshake(connection, ref record, originalMessage);
        }

        //Send a HelloVerifyRequest handshake message to a peer
        private void SendHelloVerifyRequest(IPEndPoint peerAddress, ulong recordSequence, ushort epoch, IRecordProtection recordProtection, ProtocolVersion protocolVersion)
        {
            Handshake handshake = new Handshake();
            handshake.MessageType = HandshakeType.HelloVerifyRequest;
            handshake.Length = HelloVerifyRequest.Size;
            handshake.MessageSequence = 0;
            handshake.FragmentOffset = 0;
            handshake.FragmentLength = handshake.Length;

            int plaintextPayloadSize = Handshake.Size + (int)handshake.Length;

            Record record = new Record();
            record.ContentType = ContentType.Handshake;
            record.ProtocolVersion = protocolVersion;
            record.Epoch = epoch;
            record.SequenceNumber = recordSequence;
            record.Length = (ushort)recordProtection.GetEncryptedSize(plaintextPayloadSize);

            // Encode record to wire format
            ByteSpan packet = new byte[Record.Size + record.Length];
            ByteSpan writer = packet;
            record.Encode(writer);
            writer = writer.Slice(Record.Size);
            handshake.Encode(writer);
            writer = writer.Slice(Handshake.Size);
            HelloVerifyRequest.Encode(writer, peerAddress, this.CurrentCookieHmac, protocolVersion);

            // Protect record payload
            recordProtection.EncryptServerPlaintext(
                packet.Slice(Record.Size, record.Length),
                packet.Slice(Record.Size, plaintextPayloadSize),
                ref record
            );

            this.sendQueue.TryAdd(new SendMessageInfo(packet, peerAddress));
        }

        /// <summary>
        /// Handle a request to send a datagram to the network
        /// </summary>
        internal void EncryptAndSendAppData(ByteSpan span, LocklessDtlsServerConnection connection)
        {
            PeerData peer = connection.PeerData;
            if (peer == null)
            {
                // Drop messages if we don't know how to send them
                return;
            }

            // If we're negotiating a new epoch, queue data
            if (peer.Epoch == 0 || peer.NextEpoch.State != HandshakeState.ExpectingHello)
            {
                ByteSpan copyOfSpan = new byte[span.Length];
                span.CopyTo(copyOfSpan);

                peer.QueuedApplicationDataMessage.Add(copyOfSpan);
                return;
            }

            // Send any queued application data now
            for (int ii = 0, nn = peer.QueuedApplicationDataMessage.Count; ii != nn; ++ii)
            {
                ByteSpan queuedSpan = peer.QueuedApplicationDataMessage[ii];

                QueueAppDataRecord(queuedSpan, connection, peer);
            }

            peer.QueuedApplicationDataMessage.Clear();

            QueueAppDataRecord(span, connection, peer);
        }

        private void QueueAppDataRecord(ByteSpan span, LocklessDtlsServerConnection connection, PeerData peer)
        {
            Record outgoingRecord = new Record();
            outgoingRecord.ContentType = ContentType.ApplicationData;
            outgoingRecord.ProtocolVersion = peer.ProtocolVersion;
            outgoingRecord.Epoch = peer.Epoch;
            outgoingRecord.SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence;
            outgoingRecord.Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(span.Length);
                        
            ++peer.CurrentEpoch.NextOutgoingSequence;

            // Encode the record to wire format
            ByteSpan packet = new byte[Record.Size + outgoingRecord.Length];
            ByteSpan writer = packet;
            outgoingRecord.Encode(writer);
            writer = writer.Slice(Record.Size);
            span.CopyTo(writer);

            // Protect the record
            peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
                packet.Slice(Record.Size, outgoingRecord.Length),
                packet.Slice(Record.Size, span.Length),
                ref outgoingRecord
            );

            connection.Statistics.LogPacketSend(packet.Length);
            this.sendQueue.TryAdd(new SendMessageInfo(packet, connection.EndPoint));
        }
    }
}