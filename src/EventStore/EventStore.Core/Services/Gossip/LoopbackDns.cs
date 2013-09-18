using System;
using System.Net;

namespace EventStore.Core.Services.Gossip
{
    public class ConfigDns : IDnsService
    {
        private readonly IPAddress[] _ipAddresses;

        public ConfigDns(IPAddress[] ipAddresses)
        {
            if (ipAddresses == null)
                throw new ArgumentNullException("ipAddresses");
            _ipAddresses = ipAddresses;
        }

        public IAsyncResult BeginGetHostAddresses(string hostNameOrAddress, AsyncCallback requestCallback, object state)
        {
            requestCallback(null);
            return null;
        }

        public IPAddress[] EndGetHostAddresses(IAsyncResult asyncResult)
        {
            return _ipAddresses;
        }
    }
}