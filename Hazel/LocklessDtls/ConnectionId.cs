using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Hazel.Dtls
{
    public struct ConnectionId : IEquatable<ConnectionId>
    {
        public IPEndPoint EndPoint;
        public int Serial;

        public static ConnectionId Create(IPEndPoint endPoint, int serial)
        {
            return new ConnectionId
            {
                EndPoint = endPoint,
                Serial = serial,
            };
        }

        public bool Equals(ConnectionId other)
        {
            return this.Serial == other.Serial
                && this.EndPoint.Equals(other.EndPoint)
                ;
        }

        public override bool Equals(object obj)
        {
            if (obj is ConnectionId)
            {
                return this.Equals((ConnectionId)obj);
            }

            return false;
        }

        public override int GetHashCode()
        {
            ///NOTE(mendsley): We're only hashing the endpoint
            /// here, as the common case will have one
            /// connection per address+port tuple.
            return this.EndPoint.GetHashCode();
        }
    }
}
