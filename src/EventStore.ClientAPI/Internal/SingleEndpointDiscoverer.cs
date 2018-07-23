using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using EventStore.ClientAPI.Internal;

namespace EventStore.ClientApi.Core.Internal
{
    internal class SingleEndpointDiscoverer : IEndPointDiscoverer
    {
        private readonly Uri _uri;
        private readonly bool _useSslConnection;

        public SingleEndpointDiscoverer(Uri uri, bool useSslConnection)
        {
            _uri = uri;
            _useSslConnection = useSslConnection;
        }

        public async Task<NodeEndPoints> DiscoverAsync(IPEndPoint failedTcpEndPoint)
        {
            var endpoint = await GetSingleNodeIPEndPointFrom(_uri).ConfigureAwait(false);
            return new NodeEndPoints(_useSslConnection ? null : endpoint, _useSslConnection ? endpoint : null);
        }

        private static async Task<IPEndPoint> GetSingleNodeIPEndPointFrom(Uri uri)
        {
            var ipaddress = IPAddress.Any;
            if (!IPAddress.TryParse(uri.Host, out ipaddress))
            {
                var entries = await Dns.GetHostAddressesAsync(uri.Host);
                if (entries.Length == 0) throw new Exception(string.Format("Unable to parse IP address or lookup DNS host for '{0}'", uri.Host));
                //pick an IPv4 address, if one exists
                ipaddress = entries.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                if (ipaddress == null) throw new Exception(string.Format("Could not get an IPv4 address for host '{0}'", uri.Host));
            }
            var port = uri.IsDefaultPort ? 2113 : uri.Port;
            return new IPEndPoint(ipaddress, port);
        }
    }
}
