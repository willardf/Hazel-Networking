using Hazel.Dtls;
using System.Net;

namespace Hazel.UnitTests.Dtls
{
    internal class TestDtlsHandshakeDropUnityConnection : DtlsUnityConnection
    {
        public int DropSendClientKeyExchangeFlightCount = 0;

        public TestDtlsHandshakeDropUnityConnection(ILogger logger, IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4) : base(logger, remoteEndPoint, ipMode)
        {

        }

        protected override bool DropClientKeyExchangeFlight()
        {
            if (DropSendClientKeyExchangeFlightCount > 0)
            {
                this.logger.WriteInfo($"Dropping SendClientKeyExchangeFlight");
                --DropSendClientKeyExchangeFlightCount;
                return true;
            }

            return false;
        }
    }
}
