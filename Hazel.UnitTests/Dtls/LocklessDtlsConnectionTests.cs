using Hazel.Dtls;
using Hazel.Udp;
using Hazel.Udp.FewerThreads;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Hazel.UnitTests.Dtls
{
    [TestClass]
    public class LocklessDtlsConnectionTests
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

        private LocklessDtlsConnectionListener CreateListener(int numWorkers, IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            LocklessDtlsConnectionListener listener = new LocklessDtlsConnectionListener(2, endPoint, logger, ipMode);
            listener.SetCertificate(GetCertificateForServer());
            return listener;

        }

        private ThreadLimitedDtlsUnityConnection CreateConnection(IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
        {
            ThreadLimitedDtlsUnityConnection connection = new ThreadLimitedDtlsUnityConnection(logger, endPoint, ipMode);
            connection.SetValidServerCertificates(GetCertificateForClient());
            return connection;
        }

        private ThreadLimitedDtlsUnityConnection[] CreateConnections(int num, IPEndPoint endPoint, IPMode ipMode = IPMode.IPv4)
        {
            ThreadLimitedDtlsUnityConnection[] output = new ThreadLimitedDtlsUnityConnection[num];
            for (int i = 0; i < output.Length; ++i)
            {
                output[i] = CreateConnection(endPoint, new TestLogger("Client " + i), ipMode);
            }

            return output;
        }

        private void DisposeAllConnections(UdpConnection[] connections)
        {
            foreach (var conn in connections)
            {
                conn.Dispose();
            }
        }

        [TestMethod]
        public void DtlsServerDisposeDoesNotDisconnectTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 27510);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            Semaphore signal = new Semaphore(0, int.MaxValue);

            using (var listener = CreateListener(2, new IPEndPoint(IPAddress.Any, ep.Port), new TestLogger()))
            using (var connection = CreateConnection(ep, new TestLogger()))
            {
                listener.NewConnection += (evt) =>
                {
                    serverConnected = true;
                    signal.Release();
                    evt.Connection.Disconnected += (o, et) =>
                    {
                        serverDisconnected = true;
                    };
                };
                connection.Disconnected += (o, evt) =>
                {
                    clientDisconnected = true;
                    signal.Release();
                };

                listener.Start();
                connection.Connect();

                // wait for the client to connect
                signal.WaitOne(100);

                Assert.AreEqual(connection.State, ConnectionState.Connected);

                listener.Dispose();

                // wait for the client to disconnect
                Assert.IsFalse(signal.WaitOne(100));

                Assert.IsTrue(serverConnected, "Server connect event should fire");
                Assert.IsFalse(clientDisconnected, "Client disconnect event shouldn't fire");
                Assert.IsFalse(serverDisconnected, "Server disconnect event shouldn't fire");
                Assert.AreEqual(0, listener.ConnectionCount);
            }
        }

        class MalformedDTLSListener : LocklessDtlsConnectionListener
        {
            public MalformedDTLSListener(int numWorkers, IPEndPoint endPoint, ILogger logger, IPMode ipMode = IPMode.IPv4)
                : base(numWorkers, endPoint, logger, ipMode)
            {
            }

            public void InjectPacket(ByteSpan packet, LocklessDtlsServerConnection connection)
            {
                MessageReader reader = MessageReader.GetSized(packet.Length);
                reader.Length = packet.Length;
                Array.Copy(packet.GetUnderlyingArray(), packet.Offset, reader.Buffer, reader.Offset, packet.Length);

                this.EnqueueMessageReceived(reader, connection);
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

            LocklessDtlsServerConnection serverConnection = null;

            Semaphore signal = new Semaphore(0, int.MaxValue);

            using (MalformedDTLSListener listener = new MalformedDTLSListener(2, new IPEndPoint(IPAddress.Any, ep.Port), new TestLogger()))
            using (DtlsUnityConnection connection = new DtlsUnityConnection(new TestLogger(), ep))
            {
                listener.SetCertificate(GetCertificateForServer());
                connection.SetValidServerCertificates(GetCertificateForClient());

                listener.NewConnection += (evt) =>
                {
                    serverConnection = (LocklessDtlsServerConnection)evt.Connection;

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
                record.ProtocolVersion = ProtocolVersion.DTLS1_2;
                record.Epoch = 1;
                record.SequenceNumber = 10;
                record.Length = (ushort)data.Length;

                ByteSpan encoded = new byte[Record.Size + data.Length];
                record.Encode(encoded);
                data.CopyTo(encoded.Slice(Record.Size));

                listener.InjectPacket(encoded, serverConnection);

                // wait for the client to disconnect
                listener.Dispose();
                signal.WaitOne(100);
            }
        }

        [TestMethod]
        public void TestMalformedConnectionData()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 27510);

            Connection serverConnection = null;
            bool clientDisconnected = false;

            using (LocklessDtlsConnectionListener listener = new LocklessDtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, ep.Port), new TestLogger()))
            using (MalformedDTLSClient connection = new MalformedDTLSClient(new TestLogger(), ep))
            {
                listener.SetCertificate(GetCertificateForServer());
                connection.SetValidServerCertificates(GetCertificateForClient());

                listener.NewConnection += (evt) =>
                {
                    serverConnection = evt.Connection;
                };

                connection.Disconnected += (o, evt) => {
                    clientDisconnected = true;
                };

                listener.Start();
                connection.Connect();

                Assert.IsTrue(listener.ReceiveThreadRunning, "Listener should be able to handle a malformed hello packet");
                Assert.AreEqual(ConnectionState.Disconnected, connection.State);
                Assert.IsNull(serverConnection, "Server NewConnection event should not fire");
                Assert.IsTrue(clientDisconnected, "Client disconnect event should fire");

                Assert.AreEqual(1, listener.ConnectionCount, "We expect the listener to count this connection until it times out server-side.");
            }
        }


        [TestMethod]
        public void TestReorderedHandshakePacketsConnects()
        {
            IPEndPoint captureEndPoint = new IPEndPoint(IPAddress.Loopback, 27511);
            IPEndPoint listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 27510);

            Semaphore signal = new Semaphore(0, int.MaxValue);

            var logger = new TestLogger("Throttle");

            using (SocketCapture capture = new SocketCapture(captureEndPoint, listenerEndPoint, logger))
            using (LocklessDtlsConnectionListener listener = new LocklessDtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, listenerEndPoint.Port), new TestLogger("Server")))
            using (DtlsUnityConnection connection = new DtlsUnityConnection(new TestLogger("Client "), captureEndPoint))
            {
                Semaphore listenerToConnectionThrottle = new Semaphore(0, int.MaxValue);
                capture.SendToLocalSemaphore = listenerToConnectionThrottle;
                Thread throttleThread = new Thread(() => {
                    // HelloVerifyRequest
                    capture.AssertPacketsToLocalCountEquals(1);
                    listenerToConnectionThrottle.Release(1);

                    // ServerHello, Server Certificate (Fragment)
                    // Server Cert
                    // ServerKeyExchange, ServerHelloDone
                    capture.AssertPacketsToLocalCountEquals(3);
                    capture.ReorderPacketsForLocal(list => list.Swap(0, 1));
                    listenerToConnectionThrottle.Release(3);
                    capture.AssertPacketsToLocalCountEquals(0);

                    // Same flight, let's swap the ServerKeyExchange to the front
                    capture.AssertPacketsToLocalCountEquals(3);
                    capture.ReorderPacketsForLocal(list => list.Swap(0, 2));
                    listenerToConnectionThrottle.Release(3);
                    capture.AssertPacketsToLocalCountEquals(0);

                    // Same flight, no swap we do matters as long as the ServerKeyExchange gets through.
                    capture.AssertPacketsToLocalCountEquals(3);
                    capture.ReorderPacketsForLocal(list => list.Reverse());

                    capture.SendToLocalSemaphore = null;
                    listenerToConnectionThrottle.Release(1);
                });
                throttleThread.Start();

                listener.SetCertificate(GetCertificateForServer());
                connection.SetValidServerCertificates(GetCertificateForClient());

                listener.NewConnection += (evt) =>
                {
                    signal.Release();
                };

                listener.Start();
                connection.Connect();

                Assert.IsTrue(signal.WaitOne(100), "Server NewConnection should fire");
                Assert.AreEqual(connection.State, ConnectionState.Connected);
            }
        }


        [TestMethod]
        public void TestResentClientHelloConnects()
        {
            IPEndPoint captureEndPoint = new IPEndPoint(IPAddress.Loopback, 27511);
            IPEndPoint listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 27510);

            Semaphore signal = new Semaphore(0, int.MaxValue);

            var logger = new TestLogger("Throttle");

            using (SocketCapture capture = new SocketCapture(captureEndPoint, listenerEndPoint, logger))
            using (LocklessDtlsConnectionListener listener = new LocklessDtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, listenerEndPoint.Port), new TestLogger("Server")))
            using (DtlsUnityConnection connection = new DtlsUnityConnection(new TestLogger("Client "), captureEndPoint))
            {
                Semaphore listenerToConnectionThrottle = new Semaphore(0, int.MaxValue);
                capture.SendToLocalSemaphore = listenerToConnectionThrottle;
                Thread throttleThread = new Thread(() => {
                    // Trigger resend of HelloVerifyRequest
                    capture.DiscardPacketForLocal();

                    capture.AssertPacketsToLocalCountEquals(1);
                    listenerToConnectionThrottle.Release(1);

                    // ServerHello, ServerCertificate
                    // ServerCertificate
                    // ServerKeyExchange, ServerHelloDone
                    capture.AssertPacketsToLocalCountEquals(3);
                    listenerToConnectionThrottle.Release(3);

                    // Trigger a resend of ServerKeyExchange, ServerHelloDone
                    capture.DiscardPacketForLocal();
                    
                    // From here, flush everything. We recover or not.
                    capture.SendToLocalSemaphore = null;
                    listenerToConnectionThrottle.Release(1);
                });
                throttleThread.Start();

                listener.SetCertificate(GetCertificateForServer());
                connection.SetValidServerCertificates(GetCertificateForClient());

                listener.NewConnection += (evt) =>
                {
                    signal.Release();
                };

                listener.Start();
                connection.Connect();

                Assert.IsTrue(signal.WaitOne(100), "Server NewConnection should fire");
                Assert.AreEqual(connection.State, ConnectionState.Connected);
            }
        }

        [TestMethod]
        public void TestResentServerHelloConnects()
        {
            IPEndPoint captureEndPoint = new IPEndPoint(IPAddress.Loopback, 27511);
            IPEndPoint listenerEndPoint = new IPEndPoint(IPAddress.Loopback, 27510);

            Semaphore signal = new Semaphore(0, int.MaxValue);

            using (SocketCapture capture = new SocketCapture(captureEndPoint, listenerEndPoint))
            using (LocklessDtlsConnectionListener listener = new LocklessDtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, listenerEndPoint.Port), new TestLogger("Server")))
            using (DtlsUnityConnection connection = new DtlsUnityConnection(new TestLogger("Client "), captureEndPoint))
            {
                Semaphore listenerToConnectionThrottle = new Semaphore(0, int.MaxValue);
                capture.SendToLocalSemaphore = listenerToConnectionThrottle;
                Thread throttleThread = new Thread(() => {
                    // HelloVerifyRequest
                    capture.AssertPacketsToLocalCountEquals(1);
                    listenerToConnectionThrottle.Release(1);

                    // ServerHello, Server Certificate
                    // Server Certificate
                    // ServerKeyExchange, ServerHelloDone
                    capture.AssertPacketsToLocalCountEquals(3);
                    capture.DiscardPacketForLocal();
                    listenerToConnectionThrottle.Release(2);

                    // Wait for the resends and recover
                    capture.AssertPacketsToLocalCountEquals(3);

                    capture.SendToLocalSemaphore = null;
                    listenerToConnectionThrottle.Release(3);
                });
                throttleThread.Start();

                listener.SetCertificate(GetCertificateForServer());
                connection.SetValidServerCertificates(GetCertificateForClient());

                listener.NewConnection += (evt) =>
                {
                    signal.Release();
                };

                listener.Start();
                connection.Connect();

                Assert.IsTrue(signal.WaitOne(100), "Server NewConnection should fire");
                Assert.AreEqual(connection.State, ConnectionState.Connected);
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
            using (LocklessDtlsConnectionListener listener = new LocklessDtlsConnectionListener(2, new IPEndPoint(IPAddress.Any, listenerEndPoint.Port), new TestLogger()))
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

                listener.DisconnectAll();

                // wait for the client to disconnect
                signal.WaitOne(100);

                Assert.IsTrue(serverConnected);
                Assert.IsTrue(clientDisconnected);
                Assert.IsTrue(serverDisconnected);
            }
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [TestMethod]
        public void ServerPingDisconnectsTest()
        {
#if DEBUG
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 27510);
            using (var listener = CreateListener(2, new IPEndPoint(IPAddress.Any, ep.Port), new TestLogger("Server")))
            {
                UdpConnection serverConn = null;
                listener.NewConnection += (evt) =>
                {
                    var conn = (UdpConnection)evt.Connection;
                    conn.OnInternalDisconnect = (err) =>
                    {
                        Console.WriteLine("Disconnected for " + err);
                        return null;
                    };

                    conn.KeepAliveInterval = 100;
                    conn.MissingPingsUntilDisconnect = 3;

                    serverConn = conn;
                };

                listener.Start();

                for (int i = 0; i < 5; ++i)
                {
                    using (var connection = CreateConnection(ep, new TestLogger("Client " + i)))
                    {
                        // Server should disconnect us, client should be patient
                        connection.KeepAliveInterval = int.MaxValue;
                        connection.MissingPingsUntilDisconnect = int.MaxValue;
                        connection.Connect(timeout: 1000);

                        Assert.AreEqual(ConnectionState.Connected, connection.State);

                        // After connecting, quietly stop responding to all messages to fake connection loss.
                        serverConn.TestDropRate = 1;

                        Thread.Sleep(500);    //Enough time for ~3 keep alive packets

                        Assert.AreEqual(ConnectionState.Disconnected, connection.State);
                    }
                }

                Assert.AreEqual(0, listener.ConnectionCount, "All clients disconnected, peer count should be zero.");
            }
#else
            Assert.Inconclusive("Only works in DEBUG");
#endif
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [TestMethod]
        public void ClientPingDisconnectsTest()
        {
#if DEBUG
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 27510);
            using (var listener = CreateListener(2, new IPEndPoint(IPAddress.Any, ep.Port), new TestLogger("Server")))
            {
                // Client should disconnect us, server should be patient
                listener.NewConnection += (evt) =>
                {
                    var conn = (UdpConnection)evt.Connection;
                    conn.KeepAliveInterval = int.MaxValue;
                    conn.MissingPingsUntilDisconnect = int.MaxValue;
                };

                listener.Start();

                for (int i = 0; i < 5; ++i)
                {
                    using (var connection = CreateConnection(ep, new TestLogger("Client " + i)))
                    {
                        connection.KeepAliveInterval = 100;
                        connection.MissingPingsUntilDisconnect = 3;
                        connection.Connect(timeout: 1000);

                        Thread.Sleep(10);

                        Assert.AreEqual(ConnectionState.Connected, connection.State);

                        // After connecting, quietly stop responding to all messages to fake connection loss.
                        connection.TestDropRate = 1;

                        Thread.Sleep(500);    //Enough time for ~3 keep alive packets

                        Assert.AreEqual(ConnectionState.Disconnected, connection.State);
                    }
                }

                Assert.AreEqual(0, listener.ConnectionCount, "All clients disconnected, peer count should be zero.");
            }
#else
            Assert.Inconclusive("Only works in DEBUG");
#endif
        }

        [TestMethod]
        public void ServerDisconnectAllTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("SERVER")))
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
                listener.DisconnectAll();
                Thread.Sleep(100);

                Assert.IsTrue(serverConnected, "Server connect event should fire");
                Assert.IsTrue(clientDisconnected, "Client disconnect event should fire");
                Assert.IsTrue(serverDisconnected, "Server disconnect event shouldn't fire");
            }
        }

        [TestMethod]
        public void ClientDisposeDisconnectTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
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

            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
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
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
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
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
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
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                listener.Start();

                connection.Connect();

                Assert.AreEqual(ConnectionState.Connected, connection.State);
            }
        }

        private class ExchangeData
        {
            public int[] Count;
            public ManualResetEventSlim Event;

            public ExchangeData(int numClients)
            {
                this.Count = new int[numClients];
                this.Event = new ManualResetEventSlim();
            }
        }

        /// <summary>
        ///  Tests send and receiving with concurrent clients
        /// </summary>
        [TestMethod]
        public void DtlsMultithreadedExchangeTest()
        {
            const int NumClients = 4;
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("Server")))
            {
                Connection[] serverConnections = new Connection[NumClients];
                listener.NewConnection += (ncArgs) =>
                    {
                        // Since we're expecting to be spammed, disable resends and never drop for missing acks/pings. They just add more spam.
                        var udpConn = (UdpConnection)ncArgs.Connection;
                        udpConn.ResendTimeoutMs = int.MaxValue;
                        udpConn.DisconnectTimeoutMs = int.MaxValue;
                        udpConn.KeepAliveInterval = int.MaxValue;
                        udpConn.Disconnected += (object sender, DisconnectedEventArgs dcArgs) =>
                        {
                            Console.WriteLine("Server disconnected a client because " + dcArgs.Reason);
                        };

                        udpConn.DataReceived += (DataReceivedEventArgs data) =>
                        {
                            var tid = data.Message.ReadInt32();
                            data.Message.Recycle();
                            var msg = MessageWriter.Get(SendOption.Reliable);
                            msg.Write(tid);

                            for (int i = 0; i < serverConnections.Length; ++i)
                            {
                                var conn = serverConnections[i];
                                conn?.Send(msg);
                            }

                            msg.Recycle();
                        };

                        serverConnections[ncArgs.HandshakeData.ReadByte()] = udpConn;
                    };

                UdpConnection[] connections = null;
                try
                {
                    connections = this.CreateConnections(NumClients, new IPEndPoint(IPAddress.Loopback, 4296));

                    listener.Start();

                    // Maps MyTid to count of tid received
                    Dictionary<int, ExchangeData> dictionary = new Dictionary<int, ExchangeData>();
                    Thread[] threads = new Thread[NumClients];
                    for (int tid = 0; tid < threads.Length; tid++)
                    {
                        int myTid = tid;
                        var connection = connections[tid];
                        var myArray = dictionary[myTid] = new ExchangeData(NumClients);

                        // Set everyone up first
                        connection.ResendTimeoutMs = int.MaxValue;
                        connection.DisconnectTimeoutMs = int.MaxValue;
                        connection.KeepAliveInterval = int.MaxValue;
                        connection.Disconnected += (object sender, DisconnectedEventArgs dcArgs) =>
                        {
                            Console.WriteLine("Client disconnected because " + dcArgs.Reason);
                        };

                        connection.DataReceived += (DataReceivedEventArgs data) =>
                        {
                            var tidReceived = data.Message.ReadInt32();
                            Interlocked.Increment(ref myArray.Count[tidReceived]);
                            if (myArray.Count.All(c => c == 1000))
                            {
                                myArray.Event.Set();
                            }
                        };

                        connection.Connect(new byte[] { (byte)myTid });
                        Assert.AreEqual(ConnectionState.Connected, connection.State);

                        threads[myTid] = new Thread(() =>
                        {
                            var msg = MessageWriter.Get(SendOption.Reliable);
                            msg.Write(myTid);

                            for (int i = 0; i < 1000; i++)
                            {
                                connection.Send(msg);
                            }

                            Console.WriteLine($"Thread {myTid} sent its stuff");

                            msg.Recycle();
                        });
                    }

                    Assert.AreEqual(NumClients, listener.ConnectionCount);
                    Thread.Sleep(1000);

                    foreach (var thread in threads)
                    {
                        thread.Start();
                    }

                    foreach (var thread in threads)
                    {
                        thread.Join();
                    }

                    TestHelper.WaitAll(dictionary.Values.Select(e => e.Event), TimeSpan.FromSeconds(30));

                    for (int tid = 0; tid < threads.Length; tid++)
                    {
                        var tidsRecieved = dictionary[tid];
                        foreach (var cnt in tidsRecieved.Count)
                        {
                            Assert.AreEqual(1000, cnt);
                        }
                    }

                    Console.WriteLine("Test complete");
                }
                finally
                {
                    this.DisposeAllConnections(connections);
                }
            }
        }

        [TestMethod]
        public void DtlsSessionV0ConnectionTest()
        {
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (var connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
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
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("Server")))
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
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("Server")))
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
            using (var listener2 = this.CreateListener(4, new IPEndPoint(IPAddress.IPv6Any, 4296), new TestLogger(), IPMode.IPv6))
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
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.IPv6Any, 4296), new TestLogger(), IPMode.IPv6))
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
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
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
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
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
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
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
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                TestHelper.RunClientToServerTest(listener, connection, 10, SendOption.Reliable);
            }
        }

        [TestMethod]
        public void KeepAliveClientTest()
        {
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
            using (var connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger()))
            {
                listener.Start();

                connection.Connect();
                connection.KeepAliveInterval = 100;

                Thread.Sleep(1050);    //Enough time for ~10 keep alive packets

                Assert.AreEqual(ConnectionState.Connected, connection.State);
                Assert.IsTrue(
                    connection.Statistics.PingMessagesSent >= 9,
                    "Pings Sent: " + connection.Statistics.PingMessagesSent
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

            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
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
                    client.Statistics.PingMessagesSent >= 9,
                    "Pings Sent: " + client.Statistics.PingMessagesSent
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

                Connection serverConnection = null;
                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    serverConnection = args.Connection;
                    serverConnection.Disconnected += delegate (object sender2, DisconnectedEventArgs args2)
                    {
                        mutex2.Set();
                    };

                    mutex.Set();
                };

                listener.Start();

                connection.Connect();

                Assert.AreEqual(ConnectionState.Connected, connection.State);

                mutex.WaitOne(1000);
                Assert.AreEqual(ConnectionState.Connected, serverConnection.State);

                connection.Disconnect("Testing");
                Assert.AreEqual(ConnectionState.Disconnected, connection.State);

                mutex2.WaitOne(1000);
                Assert.AreEqual(ConnectionState.Disconnected, serverConnection.State);
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [TestMethod]
        public void ServerDisconnectTest()
        {
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger("Server")))
            using (UdpConnection connection = this.CreateConnection(new IPEndPoint(IPAddress.Loopback, 4296), new TestLogger("Client")))
            {
                SemaphoreSlim sem = new SemaphoreSlim(0, 100);
                ManualResetEventSlim serverMutex = new ManualResetEventSlim(false);

                connection.Disconnected += delegate (object sender, DisconnectedEventArgs args)
                {
                    sem.Release();
                };

                listener.NewConnection += delegate (NewConnectionEventArgs args)
                {
                    sem.Release();

                    // This has to be on a new thread because the client will go straight from Connecting to Disconnected
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        serverMutex.Wait(500);
                        args.Connection.Disconnect("Testing");
                    });
                };

                listener.Start();

                connection.Connect();

                Assert.IsTrue(sem.Wait(500), "Didn't connect");
                Assert.AreEqual(ConnectionState.Connected, connection.State);

                serverMutex.Set();

                Assert.IsTrue(sem.Wait(500), "Didn't disconnect");
                Assert.AreEqual(ConnectionState.Disconnected, connection.State);
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [TestMethod]
        public void ServerExtraDataDisconnectTest()
        {
            using (var listener = this.CreateListener(2, new IPEndPoint(IPAddress.Any, 4296), new TestLogger()))
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
                    // This has to be on a new thread because the client will go straight from Connecting to Disconnected
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        MessageWriter writer = MessageWriter.Get(SendOption.None);
                        writer.Write("Goodbye");
                        args.Connection.Disconnect("Testing", writer);

                    });
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
