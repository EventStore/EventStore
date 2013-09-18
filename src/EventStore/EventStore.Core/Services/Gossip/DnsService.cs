using System;
using System.Net;

namespace EventStore.Core.Services.Gossip
{
    public class DnsService : IDnsService
    {
        public IAsyncResult BeginGetHostAddresses(string hostNameOrAddress, AsyncCallback requestCallback, object state)
        {
            return Dns.BeginGetHostAddresses(hostNameOrAddress, requestCallback, state);
        }

        public IPAddress[] EndGetHostAddresses(IAsyncResult asyncResult)
        {
            return Dns.EndGetHostAddresses(asyncResult);
        }
    }
}