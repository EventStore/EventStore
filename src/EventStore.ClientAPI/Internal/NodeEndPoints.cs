using System;
using System.Net;

namespace EventStore.ClientAPI.Internal
{
    internal struct NodeEndPoints
    {
        public readonly IPEndPoint TcpEndPoint;
        public readonly IPEndPoint SecureTcpEndPoint;

        public NodeEndPoints(IPEndPoint tcpEndPoint, IPEndPoint secureTcpEndPoint)
        {
            if ((tcpEndPoint ?? secureTcpEndPoint) == null) throw new ArgumentException("Both endpoints are null.");
            TcpEndPoint = tcpEndPoint;
            SecureTcpEndPoint = secureTcpEndPoint;
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}]",
                TcpEndPoint == null ? "n/a" : TcpEndPoint.ToString(),
                SecureTcpEndPoint == null ? "n/a" : SecureTcpEndPoint.ToString());
        }
    }
}