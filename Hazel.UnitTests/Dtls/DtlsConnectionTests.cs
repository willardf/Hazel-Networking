using Hazel.Dtls;
using Hazel.Udp;
using Hazel.Udp.FewerThreads;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Hazel.UnitTests.Dtls
{
    [TestClass]
    public class DtlsConnectionTests
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
            return new X509Certificate2(Utils.DecodePEM(TestCertificate)).CopyWithPrivateKey(privateKey);
        }

        private static X509Certificate2Collection GetCertificateForClient()
        {
            X509Certificate2 publicCertificate = new X509Certificate2(Utils.DecodePEM(TestCertificate));

            X509Certificate2Collection clientCertificates = new X509Certificate2Collection();
            clientCertificates.Add(publicCertificate);
            return clientCertificates;
        }

        protected DtlsConnectionListener CreateListener(int numWorkers, IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            DtlsConnectionListener listener = new DtlsConnectionListener(2, endPoint, logger, ipMode);
            listener.SetCertificate(GetCertificateForServer());
            return listener;

        }

        protected DtlsUnityConnection CreateConnection(IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            DtlsUnityConnection connection = new DtlsUnityConnection(logger, endPoint, ipMode);
            connection.SetValidServerCertificates(GetCertificateForClient());
            return connection;
        }

        [TestMethod]
        public void DtlsServerDisposeDisconnectsTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 27510);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            Semaphore signal = new Semaphore(0, int.MaxValue);

            using (var listener = (DtlsConnectionListener)CreateListener(2, new IPEndPoint(IPAddress.Any, ep.Port), new TestLogger()))
            using (var connection = CreateConnection(ep, new TestLogger()))
            {
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
                Assert.AreEqual(0, listener.PeerCount);
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

                base.ProcessIncomingMessageFromOtherThread(reader, peerAddress, connectionId);
            }
        }

        class MalformedDTLSClient : DtlsUnityConnection
        {
            public Func<ClientHello, ByteSpan, ByteSpan> EncodeClientHelloCallback = Test_CompressionLengthOverrunClientHello;

            public MalformedDTLSClient(ILogger logger, IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4) : base(logger, remoteEndPoint, ipMode)
            {

            }

            protected override void SendClientHello(bool isResend)
            {
                Test_SendClientHello(EncodeClientHelloCallback);
            }

            public static ByteSpan Test_CompressionLengthOverrunClientHello(ClientHello clientHello, ByteSpan writer)
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

                Assert.AreEqual(0, listener.PeerCount);

                // wait for the client to disconnect
                listener.Dispose();
                signal.WaitOne(100);
            }
        }


        [TestMethod]
        public void TestReorderedCertFragmentsConnects()
        {
            IPEndPoint captureEndPoint = new IPEndPoint(IPAddress.Loopback, 27511);
            IPEndPoint listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 27510);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            Semaphore signal = new Semaphore(0, int.MaxValue);

            var logger = new TestLogger("Throttle");

            using (SocketCapture capture = new SocketCapture(captureEndPoint, listenerEndPoint, logger))
            using (DtlsConnectionListener listener = new DtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, listenerEndPoint.Port), new TestLogger("Server")))
            using (DtlsUnityConnection connection = new DtlsUnityConnection(new TestLogger("Client "), captureEndPoint))
            {
                Semaphore listenerToConnectionThrottle = new Semaphore(0, int.MaxValue);
                capture.SendToLocalSemaphore = listenerToConnectionThrottle;
                Thread throttleThread = new Thread(() => {
                    // HelloVerifyRequest
                    while (capture.PacketsForLocalCount == 0) Thread.Sleep(10);
                    Assert.AreEqual(1, capture.PacketsForLocalCount);
                    listenerToConnectionThrottle.Release(1);

                    // ServerHello, Server Certificate (Fragment)
                    // Server Cert
                    // ServerKeyExchange, ServerHelloDone
                    while (capture.PacketsForLocalCount < 3) Thread.Sleep(10);
                    Assert.AreEqual(3, capture.PacketsForLocalCount);
                    capture.ReversePacketsForLocal();
                    listenerToConnectionThrottle.Release(3);

                    // From here, either we recover or we don't.
                    capture.SendToLocalSemaphore = null;
                    listenerToConnectionThrottle.Release(int.MaxValue);
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


        [TestMethod]
        public void TestResentClientHelloConnects()
        {
            IPEndPoint captureEndPoint = new IPEndPoint(IPAddress.Loopback, 27511);
            IPEndPoint listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 27510);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            Semaphore signal = new Semaphore(0, int.MaxValue);

            var logger = new TestLogger("Throttle");

            using (SocketCapture capture = new SocketCapture(captureEndPoint, listenerEndPoint, logger))
            using (DtlsConnectionListener listener = new DtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, listenerEndPoint.Port), new TestLogger("Server")))
            using (DtlsUnityConnection connection = new DtlsUnityConnection(new TestLogger("Client "), captureEndPoint))
            {
                Semaphore listenerToConnectionThrottle = new Semaphore(0, int.MaxValue);
                capture.SendToLocalSemaphore = listenerToConnectionThrottle;
                Thread throttleThread = new Thread(() => {
                    // Trigger resend of HelloVerifyRequest
                    capture.DiscardPacketForLocal();
                    Thread.Sleep(1000);
                    listenerToConnectionThrottle.Release(capture.PacketsForLocalCount); // We don't know how many resends we'll get, flush them all.

                    // ServerHello, Server Certificate
                    listenerToConnectionThrottle.Release(1);

                    // ServerHello, ServerCertificate                    
                    listenerToConnectionThrottle.Release(1);

                    // ServerKeyExchange, ServerHelloDone
                    listenerToConnectionThrottle.Release(1);

                    // Trigger a resend of ServerKeyExchange, ServerHelloDone
                    capture.DiscardPacketForLocal();
                    Thread.Sleep(1000);
                    
                    // From here, flush everything. We recover or not.
                    capture.SendToLocalSemaphore = null;
                    listenerToConnectionThrottle.Release(1);
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
            using (DtlsConnectionListener listener = new DtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, listenerEndPoint.Port), new TestLogger("Server")))
            using (DtlsUnityConnection connection = new DtlsUnityConnection(new TestLogger("Client "), captureEndPoint))
            {
                Semaphore listenerToConnectionThrottle = new Semaphore(0, int.MaxValue);
                capture.SendToLocalSemaphore = listenerToConnectionThrottle;
                Thread throttleThread = new Thread(() => {
                    // HelloVerifyRequest
                    listenerToConnectionThrottle.Release(1);

                    // ServerHello, Server Certificate
                    listenerToConnectionThrottle.Release(1);

                    // Trigger a resend of ServerHello, ServerCertificate
                    capture.DiscardPacketForLocal();
                    Thread.Sleep(1000);
                    listenerToConnectionThrottle.Release(capture.PacketsForLocalCount);

                    // ServerKeyExchange, ServerHelloDone
                    listenerToConnectionThrottle.Release(1);

                    // Trigger a resend of ServerKeyExchange, ServerHelloDone
                    Thread.Sleep(1000);

                    capture.SendToLocalSemaphore = null;
                    listenerToConnectionThrottle.Release(1);
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

        [TestMethod]
        public void TestConnectionSuccessAfterClientKeyExchangeFlightDropped()
        {
            IPEndPoint captureEndPoint = new IPEndPoint(IPAddress.Loopback, 27511);
            IPEndPoint listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 27510);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            Semaphore signal = new Semaphore(0, int.MaxValue);

            using (SocketCapture capture = new SocketCapture(captureEndPoint, listenerEndPoint))
            using (DtlsConnectionListener listener = new DtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, listenerEndPoint.Port), new TestLogger()))
            using (TestDtlsHandshakeDropUnityConnection connection = new TestDtlsHandshakeDropUnityConnection(new TestLogger(), captureEndPoint))
            {
                connection.DropSendClientKeyExchangeFlightCount = 1;

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

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [TestMethod]
        public void PingDisconnectClientTest()
        {
#if DEBUG
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 27510);
            using (DtlsConnectionListener listener = (DtlsConnectionListener)CreateListener(2, new IPEndPoint(IPAddress.Any, ep.Port), new TestLogger()))
            {
                // Adjust the ping rate to end the test faster
                listener.NewConnection += (evt) =>
                {
                    var conn = (ThreadLimitedUdpServerConnection)evt.Connection;
                    conn.KeepAliveInterval = 100;
                    conn.MissingPingsUntilDisconnect = 3;
                };

                listener.Start();

                for (int i = 0; i < 5; ++i)
                {
                    using (DtlsUnityConnection connection = (DtlsUnityConnection)CreateConnection(ep, new TestLogger()))
                    {
                        connection.KeepAliveInterval = 100;
                        connection.MissingPingsUntilDisconnect = 3;
                        connection.Connect();

                        Thread.Sleep(10);

                        // After connecting, quietly stop responding to all messages to fake connection loss.
                        connection.TestDropRate = 1;

                        Thread.Sleep(500);    //Enough time for ~3 keep alive packets

                        Assert.AreEqual(ConnectionState.NotConnected, connection.State);
                    }
                }

                listener.DisconnectOldConnections(TimeSpan.FromMilliseconds(500), null);

                Assert.AreEqual(0, listener.PeerCount, "All clients disconnected, peer count should be zero.");
            }
#else
            Assert.Inconclusive("Only works in DEBUG");
#endif
        }

        [TestMethod]
        public void ServerDisposeDisconnectsTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("SERVER")))
            using (UdpConnection connection = this.CreateConnection(ep, new TestLogger("CLIENT")))
            {
                listener.NewConnection += (evt) =>
                {
                    serverConnected = true;
                    evt.Connection.Disconnected += (o, et) => serverDisconnected = true;
                };
                connection.Disconnected += (o, evt) => clientDisconnected = true;

                listener.Start();
                connection.Connect();

                Thread.Sleep(100); // Gotta wait for the server to set up the events.
                listener.Dispose();
                Thread.Sleep(100);

                Assert.IsTrue(serverConnected);
                Assert.IsTrue(clientDisconnected);
                Assert.IsFalse(serverDisconnected);
            }
        }

        [TestMethod]
        public void ClientDisposeDisconnectTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(ep, new TestLogger()))
            {
                listener.NewConnection += (evt) =>
                {
                    serverConnected = true;
                    evt.Connection.Disconnected += (o, et) => serverDisconnected = true;
                };

                connection.Disconnected += (o, et) => clientDisconnected = true;

                listener.Start();
                connection.Connect();

                Thread.Sleep(100); // Gotta wait for the server to set up the events.
                connection.Dispose();

                Thread.Sleep(100);

                Assert.IsTrue(serverConnected);
                Assert.IsTrue(serverDisconnected);
                Assert.IsFalse(clientDisconnected);
            }
        }

        /// <summary>
        ///     Tests the fields on UdpConnection.
        /// </summary>
        [TestMethod]
        public void DtlsFieldTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(ep, new TestLogger()))
            {
                listener.Start();

                connection.Connect();

                //Connection fields
                Assert.AreEqual(ep, connection.EndPoint);

                //UdpConnection fields
                Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 4296), connection.EndPoint);
                Assert.AreEqual(1, connection.Statistics.DataBytesSent);
                Assert.AreEqual(0, connection.Statistics.DataBytesReceived);
            }
        }

        [TestMethod]
        public void DtlsHandshakeTest()
        {
            byte[] TestData = new byte[] { 1, 2, 3, 4, 5, 6 };
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                listener.Start();

                MessageReader output = null;
                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    output = e.HandshakeData.Duplicate();
                };

                connection.Connect(TestData);

                Thread.Sleep(10);
                for (int i = 0; i < TestData.Length; ++i)
                {
                    Assert.AreEqual(TestData[i], output.ReadByte());
                }
            }
        }

        [TestMethod]
        public void DtlsUnreliableMessageSendTest()
        {
            byte[] TestData = new byte[] { 1, 2, 3, 4, 5, 6 };
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                MessageReader output = null;
                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    e.Connection.DataReceived += delegate (DataReceivedEventArgs evt)
                    {
                        output = evt.Message.Duplicate();
                    };
                };

                listener.Start();
                connection.Connect();

                for (int i = 0; i < 4; ++i)
                {
                    var msg = MessageWriter.Get(SendOption.None);
                    msg.Write(TestData);
                    connection.Send(msg);
                    msg.Recycle();
                }

                Thread.Sleep(10);
                for (int i = 0; i < TestData.Length; ++i)
                {
                    Assert.AreEqual(TestData[i], output.ReadByte());
                }
            }
        }

        /// <summary>
        ///     Tests IPv4 connectivity.
        /// </summary>
        [TestMethod]
        public void DtlsIPv4ConnectionTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                listener.Start();

                connection.Connect();

                Assert.AreEqual(ConnectionState.Connected, connection.State);
            }
        }

        [TestMethod]
        public void DtlsSessionV0ConnectionTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (DtlsUnityConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                connection.HazelSessionVersion = 0;
                listener.Start();

                connection.Connect();

                Assert.AreEqual(ConnectionState.Connected, connection.State);
            }
        }

        private class MultipleClientHelloDtlsConnection : DtlsUnityConnection
        {
            public MultipleClientHelloDtlsConnection(ILogger logger, IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4) : base(logger, remoteEndPoint, ipMode)
            {
            }

            protected override void SendClientHello(bool isRetransmit)
            {
                base.SendClientHello(isRetransmit);
                base.SendClientHello(true);
            }
        }


        private class MultipleClientKeyExchangeFlightDtlsConnection : DtlsUnityConnection
        {
            public MultipleClientKeyExchangeFlightDtlsConnection(ILogger logger, IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4) : base(logger, remoteEndPoint, ipMode)
            {
            }

            protected override void SendClientKeyExchangeFlight(bool isRetransmit)
            {
                base.SendClientKeyExchangeFlight(isRetransmit);
                base.SendClientKeyExchangeFlight(true);
                base.SendClientKeyExchangeFlight(true);
            }
        }

        /// <summary>
        ///     Tests IPv4 resilience to multiple hellos.
        /// </summary>
        [TestMethod]
        public void ConnectLikeAJerkTest()
        {
            using (DtlsConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("Server")))
            using (MultipleClientHelloDtlsConnection client = new MultipleClientHelloDtlsConnection(new TestLogger("Client "), new IPEndPoint(IPAddress.Loopback, 4296), IPMode.IPv4))
            {
                client.SetValidServerCertificates(GetCertificateForClient());

                int connects = 0;
                listener.NewConnection += (obj) =>
                {
                    Interlocked.Increment(ref connects);
                };

                listener.Start();
                client.Connect(null, 1000);

                Thread.Sleep(2000);

                Assert.AreEqual(0, listener.ReceiveQueueLength);
                Assert.IsTrue(connects <= 1, $"Too many connections: {connects}");
                Assert.AreEqual(ConnectionState.Connected, client.State);
                Assert.IsTrue(client.HandshakeComplete);
            }
        }

        /// <summary>
        ///     Tests IPv4 resilience to multiple ClientKeyExchange packets.
        /// </summary>
        [TestMethod]
        public void HandshakeLikeAJerkTest()
        {
            using (DtlsConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("Server")))
            using (MultipleClientKeyExchangeFlightDtlsConnection client = new MultipleClientKeyExchangeFlightDtlsConnection(new TestLogger("Client "), new IPEndPoint(IPAddress.Loopback, 4296), IPMode.IPv4))
            {
                client.SetValidServerCertificates(GetCertificateForClient());

                int connects = 0;
                listener.NewConnection += (obj) =>
                {
                    Interlocked.Increment(ref connects);
                };

                listener.Start();
                client.Connect();

                Thread.Sleep(500);

                Assert.AreEqual(0, listener.ReceiveQueueLength);
                Assert.IsTrue(connects <= 1, $"Too many connections: {connects}");
                Assert.AreEqual(ConnectionState.Connected, client.State);
                Assert.IsTrue(client.HandshakeComplete);
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [TestMethod]
        public void MixedConnectionTest()
        {
            using (ThreadLimitedUdpConnectionListener listener2 = this.CreateListener(4, new IPEndPoint(IPAddress.IPv6Any, 4296), new TestLogger(), IPMode.IPv6))
            {
                listener2.Start();

                listener2.NewConnection += (evt) =>
                {
                    Console.WriteLine($"Connection: {evt.Connection.EndPoint}");
                };

                using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4296), new TestLogger()))
                {
                    connection.Connect();
                    Assert.AreEqual(ConnectionState.Connected, connection.State);
                }

                using (UdpConnection connection2 = this.CreateConnection(new IPEndPoint(IPAddress.IPv6Loopback, 4296), new TestLogger(), IPMode.IPv6))
                {
                    connection2.Connect();
                    Assert.AreEqual(ConnectionState.Connected, connection2.State);
                }
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [TestMethod]
        public void DtlsIPv6ConnectionTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.IPv6Any, 4296), new TestLogger(), IPMode.IPv6))
            {
                listener.Start();

                using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4296), new TestLogger(), IPMode.IPv6))
                {
                    connection.Connect();
                }
            }
        }

        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void DtlsUnreliableServerToClientTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                TestHelper.RunServerToClientTest(listener, connection, 10, SendOption.None);
            }
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void DtlsReliableServerToClientTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                TestHelper.RunServerToClientTest(listener, connection, 10, SendOption.Reliable);
            }
        }

        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void DtlsUnreliableClientToServerTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                TestHelper.RunClientToServerTest(listener, connection, 10, SendOption.None);
            }
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [TestMethod]
        public void DtlsReliableClientToServerTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                TestHelper.RunClientToServerTest(listener, connection, 10, SendOption.Reliable);
            }
        }

        [TestMethod]
        public void KeepAliveClientTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                listener.Start();

                connection.Connect();
                connection.KeepAliveInterval = 100;

                Thread.Sleep(1050);    //Enough time for ~10 keep alive packets

                Assert.AreEqual(ConnectionState.Connected, connection.State);
                Assert.IsTrue(
                    connection.Statistics.TotalBytesSent >= 500 &&
                    connection.Statistics.TotalBytesSent <= 675,
                    "Sent: " + connection.Statistics.TotalBytesSent
                );
            }
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [TestMethod]
        public void KeepAliveServerTest()
        {
            ManualResetEvent mutex = new ManualResetEvent(false);

            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                UdpConnection client = null;
                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    client = (UdpConnection)args.Connection;
                    client.KeepAliveInterval = 100;

                    Thread timeoutThread = new Thread(() =>
                    {
                        Thread.Sleep(1050);    //Enough time for ~10 keep alive packets
                        mutex.Set();
                    });
                    timeoutThread.Start();
                };

                listener.Start();

                connection.Connect();

                mutex.WaitOne();

                Assert.AreEqual(ConnectionState.Connected, client.State);

                Assert.IsTrue(
                    client.Statistics.TotalBytesSent >= 27 &&
                    client.Statistics.TotalBytesSent <= 50,
                    "Sent: " + client.Statistics.TotalBytesSent
                );
            }
        }

        /// <summary>
        ///     Tests disconnection from the client.
        /// </summary>
        [TestMethod]
        public void ClientDisconnectTest()
        {
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("Server")))
            using (var connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger("Client")))
            {
                ManualResetEvent mutex = new ManualResetEvent(false);
                ManualResetEvent mutex2 = new ManualResetEvent(false);

                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    args.Connection.Disconnected += delegate (object sender2, DisconnectedEventArgs args2)
                    {
                        mutex2.Set();
                    };

                    mutex.Set();
                };

                listener.Start();

                connection.Connect();

                Assert.AreEqual(ConnectionState.Connected, connection.State);
                mutex.WaitOne(1000);
                Assert.AreEqual(ConnectionState.Connected, connection.State);

                connection.Disconnect("Testing");

                mutex2.WaitOne(1000);
                Assert.AreEqual(ConnectionState.NotConnected, connection.State);
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [TestMethod]
        public void ServerDisconnectTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("Server")))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger("Client")))
            {
                SemaphoreSlim mutex = new SemaphoreSlim(0, 100);
                ManualResetEventSlim serverMutex = new ManualResetEventSlim(false);

                connection.Disconnected += delegate (object sender, DisconnectedEventArgs args)
                {
                    mutex.Release();
                };

                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    mutex.Release();

                    // This has to be on a new thread because the client will go straight from Connecting to NotConnected
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        serverMutex.Wait(500);
                        args.Connection.Disconnect("Testing");
                    });
                };

                listener.Start();

                connection.Connect();

                mutex.Wait(500);
                Assert.AreEqual(ConnectionState.Connected, connection.State);

                serverMutex.Set();

                mutex.Wait(500);
                Assert.AreEqual(ConnectionState.NotConnected, connection.State);
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [TestMethod]
        public void ServerExtraDataDisconnectTest()
        {
            using (ThreadLimitedUdpConnectionListener listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                string received = null;
                ManualResetEvent mutex = new ManualResetEvent(false);

                connection.Disconnected += delegate (object sender, DisconnectedEventArgs args)
                {
                    // We don't own the message, we have to read the string now
                    received = args.Message.ReadString();
                    mutex.Set();
                };

                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    MessageWriter writer = MessageWriter.Get(SendOption.None);
                    writer.Write("Goodbye");
                    args.Connection.Disconnect("Testing", writer);
                };

                listener.Start();

                connection.Connect();

                mutex.WaitOne(5000);

                Assert.IsNotNull(received);
                Assert.AreEqual("Goodbye", received);
            }
        }
    }
}
