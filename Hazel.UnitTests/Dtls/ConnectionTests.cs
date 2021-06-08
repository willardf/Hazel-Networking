using Hazel.Dtls;
using Hazel.Udp;
using Hazel.Udp.FewerThreads;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Hazel.UnitTests.Dtls
{
    [TestClass]
    public class ConnectionTests : BaseThreadLimitedUdpConnectionTests
    {
        // Created with command line
        // openssl req -newkey rsa:2048 -nodes -keyout key.pem -x509 -days 100000 -out certificate.pem
        const string TestCertificate =
@"-----BEGIN CERTIFICATE-----
MIIDbTCCAlWgAwIBAgIUREHeZ36f23eBFQ1T3sJsBwHlSBEwDQYJKoZIhvcNAQEL
BQAwRTELMAkGA1UEBhMCQVUxEzARBgNVBAgMClNvbWUtU3RhdGUxITAfBgNVBAoM
GEludGVybmV0IFdpZGdpdHMgUHR5IEx0ZDAgFw0yMTAyMDIxNDE4MTBaGA8yMjk0
MTExODE0MTgxMFowRTELMAkGA1UEBhMCQVUxEzARBgNVBAgMClNvbWUtU3RhdGUx
ITAfBgNVBAoMGEludGVybmV0IFdpZGdpdHMgUHR5IEx0ZDCCASIwDQYJKoZIhvcN
AQEBBQADggEPADCCAQoCggEBAMeHCR6Y6GFwH7ZnouxPLmqyCIJSCcfaGIuBU3k+
MG2ZyXKhhhwclL8arx5x1cGmQFvPm5wXGKSiLFChj+bW5XN7xBAc5e9KVBCEabrr
BY+X9r0a421Yjqn4F47IA2sQ6OygnttYIt0pgeEoQZhGvmc2ZfkELkptIHMavIsx
B/R0tYgtquruWveIWMtr4O/AuPxkH750SO1OxwU8gj6QXSqskrxvhl9GBzAwBKaF
W6t7yjR7eFqaGh7B55p4t5zrfYKCVgeyj5Yzr/xdvv3Q3H+0pex+JTMWrpsTaavq
F2RZYbpTOofuiTwdWbAHnXW1aFSCCIrEdEs9X2FxB73V0fcCAwEAAaNTMFEwHQYD
VR0OBBYEFETIkxnzoLXO2GcEgxTZgN8ypKowMB8GA1UdIwQYMBaAFETIkxnzoLXO
2GcEgxTZgN8ypKowMA8GA1UdEwEB/wQFMAMBAf8wDQYJKoZIhvcNAQELBQADggEB
ACZl7WQec9xLTK0paBIkVUqZKucDCXQH0JC7z4ENbiRtQvWQm6xhAlDo8Tr8oUzj
0/lft/g6wIo8dJ4jZ/iCSHnKz8qO80Gs/x5NISe9A/8Us1kq8y4nO40QW6xtQMH7
j74pcfsGKDCaMFSQZnSc93a3ZMEuVPxdI5+qsvFIeC9xxRHUNo245eLqsJAe8s1c
22Uoeu3gepozrPcIPAHADGr/CFp1HLkg9nFrTcatlNAF/N0PmLjmk/NIx/8h7n7Q
5vapNkhcyCHsW8XB5ulKmF88QZ5BdvPmtSey0t/n8ru98615G5Wb4TS2MaprzYL3
5ACeQOohFzevcQrEjjzkZAI=
-----END CERTIFICATE-----
";

        const string TestPrivateKey =
@"-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDHhwkemOhhcB+2
Z6LsTy5qsgiCUgnH2hiLgVN5PjBtmclyoYYcHJS/Gq8ecdXBpkBbz5ucFxikoixQ
oY/m1uVze8QQHOXvSlQQhGm66wWPl/a9GuNtWI6p+BeOyANrEOjsoJ7bWCLdKYHh
KEGYRr5nNmX5BC5KbSBzGryLMQf0dLWILarq7lr3iFjLa+DvwLj8ZB++dEjtTscF
PII+kF0qrJK8b4ZfRgcwMASmhVure8o0e3hamhoeweeaeLec632CglYHso+WM6/8
Xb790Nx/tKXsfiUzFq6bE2mr6hdkWWG6UzqH7ok8HVmwB511tWhUggiKxHRLPV9h
cQe91dH3AgMBAAECggEAVq+jVajHJTYqgPwLu7E3EGHi8oOj/jESAuIgGwfa0HNF
I0lr06DTOyfjt01ruiN5yKmtCKa8LSLMMAfRVlA9BexapUl42HqphTeSHARpuRYj
u8sHzgTwjoXb7kuVuJlzKQMroU5sbzvOUr1Dql3p8TugGA0p82nv9DJEghC+TQT2
GnCKhsnmwE7lb8h0z3G3yxdv3yZE0X6oFzllBGCb1O6cwsDeYsv+SyjnyUwJROGz
/VkC1+B48ALm4DhA5QIUoaRhO8vaCa7dacQTkXw1hVdLcaS9slIdXxbb9GbJvI0c
baqimIkE02VUUpmlOIKUpf1sRXy1aJFpDSvWsTNLaQKBgQD8TrcVUF7oOhX5hQer
qfNDFPvCBiWlT+8tnJraauaD1sJLD5jpRWPDu5dZ96CSZVpD1E3GQm+x58CSUknG
AUHyHfEHTpzx7elVeUj7gianmVu8r9mVKtErwPLDJ4AUhMJjEPX2Plmh9GgFck78
s2gfIvxdI+znvkH9JwGBznTIRQKBgQDKcpO7wiu025ZyPWR2p+qUh2ZlvBBr/6rg
GxbFE5VraIS6zSDVOcxjPLc1pVZ/vM2WGbda0eziLpvsyauTXMCueBiNBRWZb5E4
NK81IgbgZc4VWN9xA00cfOzO4Bjt6990BdOxiQQ1XOz1cN8DFTfsA81qR+nIne58
LhL0DmFLCwKBgCwsU92FbrhVwxcmdUtWu+JYwCMeFGU283cW3f2zjZwzc1zU5D6j
CW5xX3Q+6Hv5Bq6tcthtNUT+gDad9ZCXE8ah+1r+Jngs4Rc33tE53i6lqOwGFaAK
GQkCBP6p4cC15ZqWk5mDHQo/0h5x/uY7OtWIuIpOCeIg60i5FYh2bvfJAoGAPQ7t
i7V2ZSfNaksl37upPn7P3WMpOMl1if3hkjLj3+84CPcRLf4urMeFIkLpocEZ6Gl9
KYEjBtyz3mi8vMc+veAu12lvKEXD8MXDCi1nEYri6wFQ8s7iFPOAoKxqGGgJjv6q
6GLAyC9ssGIIgO+HXEGRVLq3wfAQG5fx03X61h0CgYEAiz3f8xiIR6PC4Gn5iMWu
wmIDk3EnxdaA+7AwN/M037jbmKfzLxA1n8jXYM+to4SJx8Fxo7MD5EDhq6UoYmPU
tGe4Ite2N9jzxG7xQrVuIx6Cg4t+E7uZ1eZuhbQ1WpqCXPIFOtXuc4szXfwD4Z+p
IsdbLCwHYD3GVgk/D7NVxyU=
-----END PRIVATE KEY-----
";
        private static X509Certificate2 GetCertificateForServer()
        {
            RSA privateKey = Utils.DecodeRSAKeyFromPEM(TestPrivateKey);
            X509Certificate2 privateCertificate = new X509Certificate2();
            privateCertificate.Import(Utils.DecodePEM(TestCertificate));
            privateCertificate.PrivateKey = privateKey;
            return privateCertificate;
        }

        private static X509Certificate2Collection GetCertificateForClient()
        {
            X509Certificate2 publicCertificate = new X509Certificate2();
            publicCertificate.Import(Utils.DecodePEM(TestCertificate));

            X509Certificate2Collection clientCertificates = new X509Certificate2Collection();
            clientCertificates.Add(publicCertificate);
            return clientCertificates;
        }

        protected override ThreadLimitedUdpConnectionListener CreateListener(int numWorkers, IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            DtlsConnectionListener listener = new DtlsConnectionListener(2, endPoint, logger, ipMode);
            listener.SetCertificate(GetCertificateForServer());
            return listener;

        }

        protected override UdpConnection CreateConnection(IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            DtlsUnityConnection connection = new DtlsUnityConnection(logger, endPoint, ipMode);
            connection.SetValidServerCertificates(GetCertificateForClient());
            return connection;
        }

        [TestMethod]
        public void TestClientConnects()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 27510);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            Semaphore signal = new Semaphore(0, int.MaxValue);

            using (DtlsConnectionListener listener = new DtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, ep.Port), new TestLogger()))
            using (DtlsUnityConnection connection = new DtlsUnityConnection(new TestLogger(), ep))
            {
                listener.SetCertificate(GetCertificateForServer());
                connection.SetValidServerCertificates(GetCertificateForClient());

                listener.NewConnection += (evt) =>
                {
                    serverConnected = true;
                    signal.Release();
                    evt.Connection.Disconnected += (o, et) => {
                        serverDisconnected = true;
                    };
                };
                connection.Disconnected += (o, evt) => {
                    clientDisconnected = true;
                    signal.Release();
                };

                listener.Start();
                connection.Connect();

                // wait for the client to connect
                signal.WaitOne(10);

                listener.Dispose();

                // wait for the client to disconnect
                signal.WaitOne(100);

                Assert.IsTrue(serverConnected);
                Assert.IsTrue(clientDisconnected);
                Assert.IsFalse(serverDisconnected);
            }
        }

        class MalformedDTLSListener : DtlsConnectionListener
        {
            public MalformedDTLSListener(int numWorkers, IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
                : base(numWorkers, endPoint, logger, ipMode)
            {
            }

            public void InjectPacket(ByteSpan packet, IPEndPoint peerAddress, ConnectionId connectionId)
            {
                MessageReader reader = MessageReader.GetSized(packet.Length);
                reader.Length = packet.Length;
                Array.Copy(packet.GetUnderlyingArray(), packet.Offset, reader.Buffer, reader.Offset, packet.Length);

                this.ProcessIncomingMessageFromOtherThread(reader, peerAddress, connectionId);
            }

            protected override void ProcessIncomingMessageFromOtherThread(MessageReader reader, IPEndPoint peerAddress, ConnectionId connectionId)
            {
                base.ProcessIncomingMessageFromOtherThread(reader, peerAddress, connectionId);
            }
        }

        class MalformedDTLSClient : DtlsUnityConnection
        {
            public MalformedDTLSClient(ILogger logger, IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4) : base(logger, remoteEndPoint, ipMode)
            {
                
            }

            protected override void SendClientHello()
            {
                Test_SendClientHello((clientHello, writer) =>
                {
                    ByteSpanBigEndianExtensions.WriteBigEndian16(writer, (ushort)ProtocolVersion.DTLS1_2);
                    writer = writer.Slice(2);

                    clientHello.Random.CopyTo(writer);
                    writer = writer.Slice(Hazel.Dtls.Random.Size);

                    // Do not encode session ids
                    writer[0] = (byte)0;
                    writer = writer.Slice(1);

                    writer[0] = (byte)clientHello.Cookie.Length;
                    clientHello.Cookie.CopyTo(writer.Slice(1));
                    writer = writer.Slice(1 + clientHello.Cookie.Length);

                    ByteSpanBigEndianExtensions.WriteBigEndian16(writer, (ushort)clientHello.CipherSuites.Length);
                    clientHello.CipherSuites.CopyTo(writer.Slice(2));
                    writer = writer.Slice(2 + clientHello.CipherSuites.Length);

                    // ============ Here is the corruption. writer[0] should be 1. ============
                    writer[0] = 255;
                    writer[1] = (byte)CompressionMethod.Null;
                    writer = writer.Slice(2);

                    // Extensions size
                    ByteSpanBigEndianExtensions.WriteBigEndian16(writer, (ushort)(6 + clientHello.SupportedCurves.Length));
                    writer = writer.Slice(2);

                    // Supported curves extension
                    ByteSpanBigEndianExtensions.WriteBigEndian16(writer, (ushort)ExtensionType.EllipticCurves);
                    ByteSpanBigEndianExtensions.WriteBigEndian16(writer, (ushort)(2 + clientHello.SupportedCurves.Length), 2);
                    ByteSpanBigEndianExtensions.WriteBigEndian16(writer, (ushort)clientHello.SupportedCurves.Length, 4);
                    clientHello.SupportedCurves.CopyTo(writer.Slice(6));

                    return writer;
                });
            }
        }

        [TestMethod]
        public void TestMalformedApplicationData()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 27510);

            IPEndPoint connectionEndPoint = ep;
            DtlsConnectionListener.ConnectionId connectionId = new ThreadLimitedUdpConnectionListener.ConnectionId();

            Semaphore signal = new Semaphore(0, int.MaxValue);

            using (MalformedDTLSListener listener = new MalformedDTLSListener(2, new IPEndPoint(IPAddress.Any, ep.Port), new TestLogger()))
            using (DtlsUnityConnection connection = new DtlsUnityConnection(new TestLogger(), ep))
            {
                listener.SetCertificate(GetCertificateForServer());
                connection.SetValidServerCertificates(GetCertificateForClient());

                listener.NewConnection += (evt) =>
                {
                    connectionEndPoint = evt.Connection.EndPoint;
                    connectionId = ((ThreadLimitedUdpServerConnection)evt.Connection).ConnectionId;

                    signal.Release();
                    evt.Connection.Disconnected += (o, et) => {
                    };
                };
                connection.Disconnected += (o, evt) => {
                    signal.Release();
                };

                listener.Start();
                connection.Connect();

                // wait for the client to connect
                signal.WaitOne(10);

                ByteSpan data = new byte[5] { 0x01, 0x02, 0x03, 0x04, 0x05 };

                Record record = new Record();
                record.ContentType = ContentType.ApplicationData;
                record.Epoch = 1;
                record.SequenceNumber = 10;
                record.Length = (ushort)data.Length;

                ByteSpan encoded = new byte[Record.Size + data.Length];
                record.Encode(encoded);
                data.CopyTo(encoded.Slice(Record.Size));

                listener.InjectPacket(encoded, connectionEndPoint, connectionId);

                // wait for the client to disconnect
                listener.Dispose();
                signal.WaitOne(100);
            }
        }

        [TestMethod]
        public void TestMalformedConnectionData()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 27510);

            IPEndPoint connectionEndPoint = ep;
            DtlsConnectionListener.ConnectionId connectionId = new ThreadLimitedUdpConnectionListener.ConnectionId();

            Semaphore signal = new Semaphore(0, int.MaxValue);

            using (DtlsConnectionListener listener = new DtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, ep.Port), new TestLogger()))
            using (MalformedDTLSClient connection = new MalformedDTLSClient(new TestLogger(), ep))
            {
                listener.SetCertificate(GetCertificateForServer());
                connection.SetValidServerCertificates(GetCertificateForClient());

                listener.NewConnection += (evt) =>
                {
                    connectionEndPoint = evt.Connection.EndPoint;
                    connectionId = ((ThreadLimitedUdpServerConnection)evt.Connection).ConnectionId;

                    signal.Release();
                    evt.Connection.Disconnected += (o, et) => {
                    };
                };
                connection.Disconnected += (o, evt) => {
                    signal.Release();
                };

                listener.Start();
                connection.Connect();

                Assert.IsTrue(listener.ReceiveThreadRunning, "Listener should be able to handle a malformed hello packet");
                Assert.AreEqual(ConnectionState.NotConnected, connection.State);

                // wait for the client to disconnect
                listener.Dispose();
                signal.WaitOne(100);
            }
        }

        [TestMethod]
        public void TestResentHandshakeConnects()
        {
            IPEndPoint captureEndPoint = new IPEndPoint(IPAddress.Loopback, 27511);
            IPEndPoint listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 27510);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            Semaphore signal = new Semaphore(0, int.MaxValue);

            using (SocketCapture capture = new SocketCapture(captureEndPoint, listenerEndPoint))
            using (DtlsConnectionListener listener = new DtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, listenerEndPoint.Port), new TestLogger()))
            using (DtlsUnityConnection connection = new DtlsUnityConnection(new TestLogger(), captureEndPoint))
            {
                Semaphore listenerToConnectionThrottle = new Semaphore(0, int.MaxValue);
                capture.SendToLocalSemaphore = listenerToConnectionThrottle;
                Thread throttleThread = new Thread(() => {
                    // HelloVerifyRequest
                    listenerToConnectionThrottle.Release(1);
                    // ServerHello, Server Certificate
                    listenerToConnectionThrottle.Release(1);

                    // Trigger a resend of ServerHello, ServerCertificate
                    Thread.Sleep(1000);
                    listenerToConnectionThrottle.Release(1);


                    // ServerKeyExchange, ServerHelloDone
                    listenerToConnectionThrottle.Release(1);

                    // Trigger a resend of ServerKeyExchange, ServerHelloDone
                    Thread.Sleep(1000);
                    listenerToConnectionThrottle.Release(1);

                    capture.SendToLocalSemaphore = null;
                });
                throttleThread.Start();

                listener.SetCertificate(GetCertificateForServer());
                connection.SetValidServerCertificates(GetCertificateForClient());

                listener.NewConnection += (evt) =>
                {
                    serverConnected = true;
                    signal.Release();
                    evt.Connection.Disconnected += (o, et) => {
                        serverDisconnected = true;
                    };
                };
                connection.Disconnected += (o, evt) => {
                    clientDisconnected = true;
                    signal.Release();
                };

                listener.Start();
                connection.Connect();

                // wait for the client to connect
                signal.WaitOne(10);

                listener.Dispose();

                // wait for the client to disconnect
                signal.WaitOne(100);

                Assert.IsTrue(serverConnected);
                Assert.IsTrue(clientDisconnected);
                Assert.IsFalse(serverDisconnected);
            }
        }
    }
}
