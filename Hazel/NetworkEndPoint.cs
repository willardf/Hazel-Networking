using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;

namespace Hazel
{
    /// <summary>
    ///     Represents an endpoint to a remote resource on a network.
    /// </summary>
    public class NetworkEndPoint : ConnectionEndPoint
    {
        /// <summary>
        ///     The EndPoint this points to.
        /// </summary>
        public EndPoint EndPoint { get; set; }

        /// <summary>
        ///     Creates a NetworkEndPoint from a given EndPoint.
        /// </summary>
        /// <param name="endPoint">The endpoint we represent./param>
        public NetworkEndPoint(EndPoint endPoint)
        {
            this.EndPoint = endPoint;
        }

        /// <summary>
        ///     Create a NetworkEndPoint to the specified address and port.
        /// </summary>
        /// <param name="address">The IP address of the server.</param>
        /// <param name="port">The port the server is listening on.</param>
        public NetworkEndPoint(IPAddress address, int port) : this(new IPEndPoint(address, port))
        {

        }

        /// <summary>
        ///     Creates a NetworkEndPoint to the specified IP address and port.
        /// </summary>
        /// <param name="IP">A valid IP address of the server.</param>
        /// <param name="port">The port the server is listening on.</param>
        public NetworkEndPoint(string IP, int port) : this(IPAddress.Parse(IP), port)
        {

        }
    }
}
