using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;
using System.Linq;

namespace EventStore.ClientAPI.Connection
{
    internal class ClusterExplorer
    {
        private readonly ILogger _log;
        private readonly HttpAsyncClient _client = new HttpAsyncClient();

        private readonly bool _allowForwarding;
        private readonly int _port;

        public ClusterExplorer(bool allowForwarding, int port)
        {
            _log = LogManager.GetLogger();

            _allowForwarding = allowForwarding;
            _port = port;
        }

        public Task<EndpointsPair?> Resolve(string dns)
        {
            var resolve = Task.Factory.StartNew(() => Dns.GetHostAddresses(dns));
            return resolve.ContinueWith(addresses => DiscoverCLuster(addresses.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private EndpointsPair? DiscoverCLuster(IPAddress[] managers)
        {
            if (managers == null || managers.Length == 0)
                throw new CannotEstablishConnectionException("DNS entry resolved to empty ip addresses list");

            var clusterInfo = GetClusterInfo(managers);
            if (clusterInfo != null && clusterInfo.Members != null && clusterInfo.Members.Any())
            {
                if (!_allowForwarding)
                {
                    _log.Info("Forwarding denied. Looking for master...");
                    var master = clusterInfo.Members.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.Master);
                    if (master == null)
                    {
                        _log.Info("Master not found");
                        return null;
                    }
                    _log.Info("Master found on [{0}:{1}, {2}:{3}]", master.ExternalTcpIp, master.ExternalTcpPort, master.ExternalHttpIp, master.ExternalHttpPort);
                    return new EndpointsPair(new IPEndPoint(IPAddress.Parse(master.ExternalTcpIp), master.ExternalTcpPort),
                                             new IPEndPoint(IPAddress.Parse(master.ExternalHttpIp), master.ExternalHttpPort));
                }

                var node = ((clusterInfo.Members.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.Master) ??
                             clusterInfo.Members.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.Slave)) ??
                             clusterInfo.Members.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.Clone)) ??
                             clusterInfo.Members.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.CatchingUp);

                if (node == null)
                {
                    _log.Info("Unable to locate master or slave or clone or catching up node");
                    return null;
                }

                _log.Info("Best choise found, it's {0} on [{1}:{2}, {3}:{4}]", 
                          node.State, 
                          node.ExternalTcpIp,
                          node.ExternalTcpPort, 
                          node.ExternalHttpIp, 
                          node.ExternalHttpPort);
                return new EndpointsPair(new IPEndPoint(IPAddress.Parse(node.ExternalTcpIp), node.ExternalTcpPort),
                                         new IPEndPoint(IPAddress.Parse(node.ExternalHttpIp), node.ExternalHttpPort));
            }

            _log.Info("Failed to discover cluster. No information available");
            return null;
        }

        private ClusterMessages.ClusterInfoDto GetClusterInfo(IPAddress[] managers)
        {
            var allInfo = new List<ClusterMessages.ClusterInfoDto>(managers.Length);
            var requestCompleted = new CountdownEvent(managers.Length);

            Action<HttpResponse> success = response =>
            {
                _log.Info("Got response from manager");
                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    _log.Info("Manager responded with {0} ({1})", response.HttpStatusCode, response.StatusDescription);
                    requestCompleted.Signal();
                    return;
                }
                try
                {
                    using (var reader = new StringReader(response.Body))
                        allInfo.Add((ClusterMessages.ClusterInfoDto)new XmlSerializer(typeof(ClusterMessages.ClusterInfoDto)).Deserialize(reader));
                }
                catch (Exception e)
                {
                    _log.Info(e, "Failed to get cluster info from manager. Deserialization error");
                }

                requestCompleted.Signal();
            };

            Action<Exception> error = e =>
            {
                _log.Info(e, "Failed to get cluster info from manager");
                requestCompleted.Signal();
            };

            foreach (var manager in managers)
            {
                var url = new IPEndPoint(manager, _port).ToHttpUrl("/gossip?format=xml");
                _log.Info("Sending gossip request to {0}...", url);
                _client.Get(url, success, error);
            }

            requestCompleted.Wait();

            _log.Info("Aggregating info about cluster...");
            return allInfo.Any() ? allInfo.Aggregate(Accumulte) : null;
        }

        private ClusterMessages.ClusterInfoDto Accumulte(ClusterMessages.ClusterInfoDto info1, ClusterMessages.ClusterInfoDto info2)
        {
            var members = info1.Members.Concat(info2.Members).ToLookup(x => new IPEndPoint(IPAddress.Parse(x.InternalHttpIp), x.InternalHttpPort))
                                   .Select(x => x.OrderByDescending(y => y.TimeStamp).First())
                                   .ToArray();
            return new ClusterMessages.ClusterInfoDto(members);
        }
    }
}
