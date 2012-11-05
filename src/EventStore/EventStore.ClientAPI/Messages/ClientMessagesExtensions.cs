using System.Net;

namespace EventStore.ClientAPI.Messages
{
    public static partial class ClientMessages
    {
        public partial class DeniedToRoute
        {
            public IPEndPoint ExternalTcpEndPoint
            {
                get
                {
                    return new IPEndPoint(IPAddress.Parse(ExternalTcpAddress), ExternalTcpPort);
                }
            }

            public IPEndPoint ExternalHttpEndPoint
            {
                get
                {
                    return new IPEndPoint(IPAddress.Parse(ExternalHttpAddress), ExternalHttpPort);
                }
            }

        }
    }
}
