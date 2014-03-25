using System.Net;

namespace EventStore.ClientAPI.Messages
{
    internal static partial class ClientMessage
    {
        public partial class NotHandled
        {
            public partial class MasterInfo
            {
                public IPEndPoint ExternalTcpEndPoint { get { return new IPEndPoint(IPAddress.Parse(ExternalTcpAddress), ExternalTcpPort); } }

                public IPEndPoint ExternalSecureTcpEndPoint
                {
                    get
                    {
                        return ExternalSecureTcpAddress == null || ExternalSecureTcpPort == null
                                ? null
                                : new IPEndPoint(IPAddress.Parse(ExternalSecureTcpAddress), ExternalSecureTcpPort.Value);
                    }
                }

                public IPEndPoint ExternalHttpEndPoint { get { return new IPEndPoint(IPAddress.Parse(ExternalHttpAddress), ExternalHttpPort); } }
            }
        }
    }
}
