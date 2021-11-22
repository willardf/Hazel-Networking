using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hazel.Dtls
{
    /// <summary>
    /// Handshake message type
    /// </summary>
    public enum HandshakeType : byte
    {
        HelloRequest = 0,
        ClientHello = 1,
        ServerHello = 2,
        HelloVerifyRequest = 3,
        Certificate = 11,
        ServerKeyExchange = 12,
        CertificateRequest = 13,
        ServerHelloDone = 14,
        CertificateVerify = 15,
        ClientKeyExchange = 16,
        Finished = 20,
    }

    /// <summary>
    /// List of cipher suites
    /// </summary>
    public enum CipherSuite
    {
        TLS_NULL_WITH_NULL_NULL = 0x0000,
        TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 = 0xC02F,
    }

    /// <summary>
    /// List of compression methods
    /// </summary>
    public enum CompressionMethod : byte
    {
        Null = 0,
    }

    /// <summary>
    /// Extension type
    /// </summary>
    public enum ExtensionType : ushort
    {
        EllipticCurves = 10,
    }

    /// <summary>
    /// Named curves
    /// </summary>
    public enum NamedCurve : ushort
    {
        Reserved = 0,
        secp256r1 = 23,
        x25519 = 29,
    }

    /// <summary>
    /// Elliptic curve type
    /// </summary>
    public enum ECCurveType : byte
    {
        NamedCurve = 3,
    }

    /// <summary>
    /// Hash algorithms
    /// </summary>
    public enum HashAlgorithm : byte
    {
        None = 0,
        Sha256 = 4,
    }

    /// <summary>
    /// Signature algorithms
    /// </summary>
    public enum SignatureAlgorithm : byte
    {
        Anonymous = 0,
        RSA = 1,
        ECDSA = 3,
    }

    /// <summary>
    /// Random state for entropy
    /// </summary>
    public struct Random
    {
        public const int Size = 0
            + 4 // gmt_unix_time
            + 28 // random_bytes
            ;
    }

    /// <summary>
    /// Encode/decode handshake protocol header
    /// </summary>
    public struct Handshake
    {
        public HandshakeType MessageType;
        public uint Length;
        public ushort MessageSequence;
        public uint FragmentOffset;
        public uint FragmentLength;

        public const int Size = 12;

        /// <summary>
        /// Parse a Handshake protocol header from wire format
        /// </summary>
        /// <returns>True if we successfully decode a handshake header. Otherwise false</returns>
        public static bool Parse(out Handshake header, ByteSpan span)
        {
            header = new Handshake();

            if (span.Length < Size)
            {
                return false;
            }

            header.MessageType = (HandshakeType)span[0];
            header.Length = span.ReadBigEndian24(1);
            header.MessageSequence = span.ReadBigEndian16(4);
            header.FragmentOffset = span.ReadBigEndian24(6);
            header.FragmentLength = span.ReadBigEndian24(9);
            return true;
        }

        /// <summary>
        /// Encode the Handshake protocol header to wire format
        /// </summary>
        /// <param name="span"></param>
        public void Encode(ByteSpan span)
        {
            span[0] = (byte)this.MessageType;
            span.WriteBigEndian24(this.Length, 1);
            span.WriteBigEndian16(this.MessageSequence, 4);
            span.WriteBigEndian24(this.FragmentOffset, 6);
            span.WriteBigEndian24(this.FragmentLength, 9);
        }
    }

    /// <summary>
    /// Encode/decode ClientHello Handshake message
    /// </summary>
    public struct ClientHello
    {
        public ByteSpan Random;
        public ByteSpan Cookie;
        public ByteSpan CipherSuites;
        public ByteSpan SupportedCurves;

        public const int MinSize = 0
            + 2 // client_version
            + Dtls.Random.Size // random
            + 1 // session_id (size)
            + 1 // cookie (size)
            + 2 // cipher_suites (size)
            + 1 // compression_methods (size)
            + 1 // compression_method[0] (NULL)

            + 2 // extensions size

            + 0 // NamedCurveList extensions[0]
            + 2 // extensions[0].extension_type
            + 2 // extensions[0].extension_data (length)
            + 2 // extensions[0].named_curve_list (size)
            ;

        /// <summary>
        /// Calculate the size in bytes required for the ClientHello payload
        /// </summary>
        /// <returns></returns>
        public int CalculateSize()
        {
            return MinSize
                + this.Cookie.Length
                + this.CipherSuites.Length
                + this.SupportedCurves.Length
                ;
        }

        /// <summary>
        /// Parse a Handshake ClientHello payload from wire format
        /// </summary>
        /// <returns>True if we successfully decode the ClientHello message. Otherwise false</returns>
        public static bool Parse(out ClientHello result, ByteSpan span)
        {
            result = new ClientHello();
            if (span.Length < MinSize)
            {
                return false;
            }

            ProtocolVersion clientVersion = (ProtocolVersion)span.ReadBigEndian16();
            if (clientVersion != ProtocolVersion.DTLS1_2)
            {
                return false;
            }
            span = span.Slice(2);

            result.Random = span.Slice(0, Dtls.Random.Size);
            span = span.Slice(Dtls.Random.Size);

            ///NOTE(mendsley): We ignore session id
            byte sessionIdSize = span[0];
            if (span.Length < 1 + sessionIdSize)
            {
                return false;
            }
            span = span.Slice(1 + sessionIdSize);

            byte cookieSize = span[0];
            if (span.Length < 1 + cookieSize)
            {
                return false;
            }
            result.Cookie = span.Slice(1, cookieSize);
            span = span.Slice(1 + cookieSize);

            ushort cipherSuiteSize = span.ReadBigEndian16();
            if (span.Length < 2 + cipherSuiteSize)
            {
                return false;
            }
            else if (cipherSuiteSize % 2 != 0)
            {
                return false;
            }
            result.CipherSuites = span.Slice(2, cipherSuiteSize);
            span = span.Slice(2 + cipherSuiteSize);

            int compressionMethodsSize = span[0];
            bool foundNullCompressionMethod = false;
            for (int ii = 0; ii != compressionMethodsSize; ++ii)
            {
                if (span[1+ii] == (byte)CompressionMethod.Null)
                {
                    foundNullCompressionMethod = true;
                    break;
                }
            }

            if (!foundNullCompressionMethod
                || span.Length < 1 + compressionMethodsSize)
            {
                return false;
            }

            span = span.Slice(1 + compressionMethodsSize);

            // Parse extensions
            if (span.Length > 0)
            {
                if (span.Length < 2)
                {
                    return false;
                }

                ushort extensionsSize = span.ReadBigEndian16();
                span = span.Slice(2);
                if (span.Length != extensionsSize)
                {
                    return false;
                }

                while (span.Length > 0)
                {
                    // Parse extension header
                    if (span.Length < 4)
                    {
                        return false;
                    }

                    ExtensionType extensionType = (ExtensionType)span.ReadBigEndian16(0);
                    ushort extensionLength = span.ReadBigEndian16(2);

                    if (span.Length < 4 + extensionLength)
                    {
                        return false;
                    }

                    ByteSpan extensionData = span.Slice(4, extensionLength);
                    span = span.Slice(4 + extensionLength);
                    result.ParseExtension(extensionType, extensionData);
                }
            }

            return true;
        }

        /// <summary>
        /// Decode a ClientHello extension
        /// </summary>
        /// <param name="extensionType">Extension type</param>
        /// <param name="extensionData">Extension data</param>
        private void ParseExtension(ExtensionType extensionType, ByteSpan extensionData)
        {
            switch (extensionType)
            {
                case ExtensionType.EllipticCurves:
                    if (extensionData.Length % 2 != 0)
                    {
                        break;
                    }
                    else if (extensionData.Length < 2)
                    {
                        break;
                    }

                    ushort namedCurveSize = extensionData.ReadBigEndian16(0);
                    if (namedCurveSize % 2 != 0)
                    {
                        break;
                    }

                    this.SupportedCurves = extensionData.Slice(2, namedCurveSize);
                    break;
            }
        }

        /// <summary>
        /// Determines if the ClientHello message advertises support
        /// for the specified cipher suite
        /// </summary>
        public bool ContainsCipherSuite(CipherSuite cipherSuite)
        {
            ByteSpan iterator = this.CipherSuites;
            while (iterator.Length >= 2)
            {
                if (iterator.ReadBigEndian16() == (ushort)cipherSuite)
                {
                    return true;
                }

                iterator = iterator.Slice(2);
            }

            return false;
        }

        /// <summary>
        /// Determines if the ClientHello message advertises support
        /// for the specified curve
        /// </summary>
        public bool ContainsCurve(NamedCurve curve)
        {
            ByteSpan iterator = this.SupportedCurves;
            while (iterator.Length >= 2)
            {
                if (iterator.ReadBigEndian16() == (ushort)curve)
                {
                    return true;
                }

                iterator = iterator.Slice(2);
            }

            return false;
        }

        /// <summary>
        /// Encode Handshake ClientHello payload to wire format
        /// </summary>
        public void Encode(ByteSpan span)
        {
            span.WriteBigEndian16((ushort)ProtocolVersion.DTLS1_2);
            span = span.Slice(2);

            Debug.Assert(this.Random.Length == Dtls.Random.Size);
            this.Random.CopyTo(span);
            span = span.Slice(Dtls.Random.Size);

            // Do not encode session ids
            span[0] = (byte)0;
            span = span.Slice(1);

            span[0] = (byte)this.Cookie.Length;
            this.Cookie.CopyTo(span.Slice(1));
            span = span.Slice(1 + this.Cookie.Length);

            span.WriteBigEndian16((ushort)this.CipherSuites.Length);
            this.CipherSuites.CopyTo(span.Slice(2));
            span = span.Slice(2 + this.CipherSuites.Length);

            span[0] = 1;
            span[1] = (byte)CompressionMethod.Null;
            span = span.Slice(2);

            // Extensions size
            span.WriteBigEndian16((ushort)(6 + this.SupportedCurves.Length));
            span = span.Slice(2);

            // Supported curves extension
            span.WriteBigEndian16((ushort)ExtensionType.EllipticCurves);
            span.WriteBigEndian16((ushort)(2 + this.SupportedCurves.Length), 2);
            span.WriteBigEndian16((ushort)this.SupportedCurves.Length, 4);
            this.SupportedCurves.CopyTo(span.Slice(6));
        }
    }

    /// <summary>
    /// Encode/decode Handshake HelloVerifyRequest message
    /// </summary>
    public struct HelloVerifyRequest
    {
        public const int CookieSize = 20;
        public const int Size = 0
            + 2 // server_version
            + 1 // cookie (size)
            + CookieSize // cookie (data)
            ;

        public ByteSpan Cookie;

        /// <summary>
        /// Parse a Handshake HelloVerifyRequest payload from wire
        /// format
        /// </summary>
        /// <returns>
        /// True if we successfully decode the HelloVerifyRequest
        /// message. Otherwise false.
        /// </returns>
        public static bool Parse(out HelloVerifyRequest result, ByteSpan span)
        {
            result = new HelloVerifyRequest();
            if (span.Length < 3)
            {
                return false;
            }

            ProtocolVersion serverVersion = (ProtocolVersion)span.ReadBigEndian16(0);
            if (serverVersion != ProtocolVersion.DTLS1_2)
            {
                return false;
            }

            byte cookieSize = span[2];
            span = span.Slice(3);

            if (span.Length < cookieSize)
            {
                return false;
            }

            result.Cookie = span;
            return true;
        }

        /// <summary>
        /// Encode a HelloVerifyRequest payload to wire format
        /// </summary>
        /// <param name="peerAddress">Address of the remote peer</param>
        /// <param name="hmac">Listener HMAC signature provider</param>
        public static void Encode(ByteSpan span, EndPoint peerAddress, HMAC hmac)
        {
            ByteSpan cookie = ComputeAddressMac(peerAddress, hmac);

            span.WriteBigEndian16((ushort)ProtocolVersion.DTLS1_2);
            span[2] = (byte)CookieSize;
            cookie.CopyTo(span.Slice(3));
        }

        /// <summary>
        /// Generate an HMAC for a peer address
        /// </summary>
        /// <param name="peerAddress">Address of the remote peer</param>
        /// <param name="hmac">Listener HMAC signature provider</param>
        public static ByteSpan ComputeAddressMac(EndPoint peerAddress, HMAC hmac)
        {
            SocketAddress address = peerAddress.Serialize();
            byte[] data = new byte[address.Size];
            for (int ii = 0, nn = data.Length; ii != nn; ++ii)
            {
                data[ii] = address[ii];
            }

            ///NOTE(mendsley): Lame that we need to allocate+copy here
            ByteSpan signature = hmac.ComputeHash(data);
            return signature.Slice(0, CookieSize);
        }

        /// <summary>
        /// Verify a client's cookie was signed by our listener
        /// </summary>
        /// <param name="cookie">Wire format cookie</param>
        /// <param name="peerAddress">Address of the remote peer</param>
        /// <param name="hmac">Listener HMAC signature provider</param>
        /// <returns>True if the cookie is valid. Otherwise false</returns>
        public static bool VerifyCookie(ByteSpan cookie, EndPoint peerAddress, HMAC hmac)
        {
            if (cookie.Length != CookieSize)
            {
                return false;
            }

            ByteSpan expectedHash = ComputeAddressMac(peerAddress, hmac);
            if (expectedHash.Length != cookie.Length)
            {
                return false;
            }

            return (1 == Crypto.Const.ConstantCompareSpans(cookie, expectedHash));
        }
    }

    /// <summary>
    /// Encode/decode Handshake ServerHello message
    /// </summary>
    public struct ServerHello
    {
        //public ProtocolVersion ServerVersion;
        public ByteSpan Random;
        public CipherSuite CipherSuite;

        public const int Size = 0
            + 2 // server_version
            + Dtls.Random.Size // random
            + 1 // session_id (size)
            + 2 // cipher_suite
            + 1 // compression_method
            ;

        /// <summary>
        /// Parse a Handshake ServerHello payload from wire format
        /// </summary>
        /// <returns>
        /// True if we successfully decode the ServerHello
        /// message. Otherwise false.
        /// </returns>
        public static bool Parse(out ServerHello result, ByteSpan span)
        {
            result = new ServerHello();
            if (span.Length < Size)
            {
                return false;
            }

            ProtocolVersion serverVersion = (ProtocolVersion)span.ReadBigEndian16();
            span = span.Slice(2);

            result.Random = span.Slice(0, Dtls.Random.Size);
            span = span.Slice(Dtls.Random.Size);

            byte sessionKeySize = span[0];
            span = span.Slice(1 + sessionKeySize);

            result.CipherSuite = (CipherSuite)span.ReadBigEndian16();
            span = span.Slice(2);

            CompressionMethod compressionMethod = (CompressionMethod)span[0];
            if (compressionMethod != CompressionMethod.Null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Encode Handshake ServerHello to wire format
        /// </summary>
        public void Encode(ByteSpan span)
        {
            Debug.Assert(this.Random.Length == Dtls.Random.Size);

            span.WriteBigEndian16((ushort)ProtocolVersion.DTLS1_2, 0);
            span = span.Slice(2);

            this.Random.CopyTo(span);
            span = span.Slice(Dtls.Random.Size);

            span[0] = 0;
            span = span.Slice(1);

            span.WriteBigEndian16((ushort)this.CipherSuite);
            span = span.Slice(2);

            span[0] = (byte)CompressionMethod.Null;
        }
    }

    /// <summary>
    /// Encode/decode Handshake Certificate message
    /// </summary>
    public struct Certificate
    {
        /// <summary>
        /// Encode a certificate to wire formate
        /// </summary>
        public static ByteSpan Encode(X509Certificate2 certificate)
        {
            ByteSpan certData = certificate.GetRawCertData();
            int totalSize = certData.Length + 3 + 3;

            ByteSpan result = new byte[totalSize];

            ByteSpan writer = result;
            writer.WriteBigEndian24((uint)certData.Length + 3);
            writer = writer.Slice(3);
            writer.WriteBigEndian24((uint)certData.Length);
            writer = writer.Slice(3);

            certData.CopyTo(writer);
            return result;
        }

        /// <summary>
        /// Parse a Handshake Certificate payload from wire format
        /// </summary>
        /// <returns>True if we successfully decode the Certificate message. Otherwise false</returns>
        public static bool Parse(out X509Certificate2 certificate, ByteSpan span)
        {
            certificate = null;
            if (span.Length < 6)
            {
                return false;
            }

            uint totalSize = span.ReadBigEndian24();
            span = span.Slice(3);

            if (span.Length < totalSize)
            {
                return false;
            }

            uint certificateSize = span.ReadBigEndian24();
            span = span.Slice(3);
            if (span.Length < certificateSize)
            {
                return false;
            }

            byte[] rawData = new byte[certificateSize];
            span.CopyTo(rawData, 0);
            try
            {
                certificate = new X509Certificate2(rawData);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Encode/decode Handshake Finished message
    /// </summary>
    public struct Finished
    {
        public const int Size = 12;
    }
}
