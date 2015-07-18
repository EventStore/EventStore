using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace EventStore.Core.Data
{
    public class GossipAdvertiseInfo
    {
        public IPEndPoint InternalTcp { get; set; }
        public IPEndPoint InternalSecureTcp { get; set; }
        public IPEndPoint ExternalTcp { get; set; }
        public IPEndPoint ExternalSecureTcp { get; set; }
        public IPEndPoint InternalHttp { get; set; }
        public IPEndPoint ExternalHttp { get; set; }
        public GossipAdvertiseInfo(IPEndPoint internalTcp, IPEndPoint internalSecureTcp,
                                   IPEndPoint externalTcp, IPEndPoint externalSecureTcp,
                                   IPEndPoint internalHttp, IPEndPoint externalHttp)
        {
            InternalTcp = internalTcp;
            InternalSecureTcp = internalSecureTcp;
            ExternalTcp = externalTcp;
            InternalHttp = internalHttp;
            ExternalHttp = externalHttp;
        }
    }
}
