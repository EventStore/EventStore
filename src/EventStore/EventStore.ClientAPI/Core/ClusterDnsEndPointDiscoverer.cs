// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using System.Linq;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

namespace EventStore.ClientAPI.Core
{
    internal class ClusterDnsEndPointDiscoverer: IEndPointDiscoverer
    {
        private readonly ILogger _log;
        private readonly string _clusterDns;
        private readonly int _maxDiscoverAttempts;
        private readonly int _managerExternalHttpPort;
        private readonly IPEndPoint[] _gossipSeeds;

        private readonly HttpAsyncClient _client;
        private ClusterMessages.MemberInfoDto[] _oldGossip;
        private TimeSpan _gossipTimeout;

        public ClusterDnsEndPointDiscoverer(ILogger log, 
                                            string clusterDns,
                                            int maxDiscoverAttempts, 
                                            int managerExternalHttpPort,
                                            IPEndPoint[] gossipSeeds,
                                            TimeSpan gossipTimeout)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");

            _log = log;
            _clusterDns = clusterDns;
            _maxDiscoverAttempts = maxDiscoverAttempts;
            _managerExternalHttpPort = managerExternalHttpPort;
            _gossipSeeds = gossipSeeds;
            _gossipTimeout = gossipTimeout;
            _client = new HttpAsyncClient(log);
        }

        public Task<NodeEndPoints> DiscoverAsync(IPEndPoint failedTcpEndPoint )
        {
            return Task.Factory.StartNew(() =>
            {
                for (int attempt = 1; attempt <= _maxDiscoverAttempts; ++attempt)
                {
                    //_log.Info("Discovering cluster. Attempt {0}/{1}...", attempt, _maxDiscoverAttempts);
                    try
                    {
                        var endPoints = DiscoverEndPoint(failedTcpEndPoint);
                        if (endPoints != null)
                        {
                            _log.Info("Discovering attempt {0}/{1} successful: best candidate is {2}.", attempt, _maxDiscoverAttempts, endPoints);
                            return endPoints.Value;
                        }

                        _log.Info("Discovering attempt {0}/{1} failed: no candidate found.", attempt, _maxDiscoverAttempts);
                    }
                    catch (Exception exc)
                    {
                        _log.Info("Discovering attempt {0}/{1} failed with error: {2}.", attempt, _maxDiscoverAttempts, exc);
                    }

                    Thread.Sleep(500);
                }
                throw new ClusterException(string.Format("Failed to discover candidate in {0} attempts.", _maxDiscoverAttempts));
            });
        }

        private NodeEndPoints? DiscoverEndPoint(IPEndPoint failedEndPoint)
        {
            var oldGossip = Interlocked.Exchange(ref _oldGossip, null);
            var gossipCandidates = oldGossip != null
                                           ? GetGossipCandidatesFromOldGossip(oldGossip, failedEndPoint)
                                           : GetGossipCandidatesFromDns();
            for (int i=0; i<gossipCandidates.Length; ++i)
            {
                var gossip = TryGetGossipFrom(gossipCandidates[i]);
                if (gossip == null || gossip.Members == null || gossip.Members.Length == 0)
                    continue;

                var bestNode = TryDetermineBestNode(gossip.Members);
                if (bestNode != null)
                {
                    _oldGossip = gossip.Members;
                    return bestNode;
                }
            }

            return null;
        }

        private IPEndPoint[] GetGossipCandidatesFromDns()
        {
            //_log.Debug("ClusterDnsEndPointDiscoverer: GetGossipCandidatesFromDns");
            IPEndPoint[] endpoints;
            if(_gossipSeeds != null && _gossipSeeds.Length > 0)
            {
                endpoints = ResolveDns(_clusterDns).Select(x => new IPEndPoint(x, _managerExternalHttpPort)).ToArray();
            } else
            {
                endpoints = _gossipSeeds;
            }
            
            RandomShuffle(endpoints, 0, endpoints.Length-1);
            return endpoints;
        }

        private IPAddress[] ResolveDns(string dns)
        {
            IPAddress[] addresses;
            try
            {
                addresses = Dns.GetHostAddresses(dns);
            }
            catch (Exception exc)
            {
                throw new ClusterException(string.Format("Error while resolving DNS entry '{0}'.", _clusterDns), exc);
            }
            if (addresses == null || addresses.Length == 0)
                throw new ClusterException(string.Format("DNS entry '{0}' resolved into empty list.", _clusterDns));
            return addresses;
        }

        private IPEndPoint[] GetGossipCandidatesFromOldGossip(IEnumerable<ClusterMessages.MemberInfoDto> oldGossip, IPEndPoint failedTcpEndPoint)
        {
            //_log.Debug("ClusterDnsEndPointDiscoverer: GetGossipCandidatesFromOldGossip, failedTcpEndPoint: {0}.", failedTcpEndPoint);
            var gossipCandidates = failedTcpEndPoint == null 
                    ? oldGossip.ToArray() 
                    : oldGossip.Where(x => !(x.ExternalTcpPort == failedTcpEndPoint.Port 
                                             && IPAddress.Parse(x.ExternalTcpIp).Equals(failedTcpEndPoint.Address)))
                               .ToArray();
            return ArrangeGossipCandidates(gossipCandidates);
        }

        private IPEndPoint[] ArrangeGossipCandidates(ClusterMessages.MemberInfoDto[] members)
        {
            var result = new IPEndPoint[members.Length];
            int i = -1;
            int j = members.Length;
            for (int k = 0; k < members.Length; ++k)
            {
                if (members[k].State == ClusterMessages.VNodeState.Manager)
                    result[--j] = new IPEndPoint(IPAddress.Parse(members[k].ExternalHttpIp), members[k].ExternalHttpPort);
                else
                    result[++i] = new IPEndPoint(IPAddress.Parse(members[k].ExternalHttpIp), members[k].ExternalHttpPort);
            }
            RandomShuffle(result, 0, i); // shuffle nodes
            RandomShuffle(result, j, members.Length - 1); // shuffle managers

            return result;
        }

        private void RandomShuffle<T>(T[] arr, int i, int j)
        {
            if (i >= j)
                return;
            var rnd = new Random(Guid.NewGuid().GetHashCode());
            for (int k = i; k <= j; ++k)
            {
                var index = rnd.Next(k, j + 1);
                var tmp = arr[index];
                arr[index] = arr[k];
                arr[k] = tmp;
            }
        }

        private ClusterMessages.ClusterInfoDto TryGetGossipFrom(IPEndPoint endPoint)
        {
            //_log.Debug("ClusterDnsEndPointDiscoverer: Trying to get gossip from [{0}].", endPoint);

            ClusterMessages.ClusterInfoDto result = null;
            var completed = new ManualResetEventSlim(false);

            var url = endPoint.ToHttpUrl("/gossip?format=json");
            _client.Get(
                url,
                null,
                _gossipTimeout,
                response =>
                {
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                    {
                        //_log.Info("[{0}] responded with {1} ({2})", endPoint, response.HttpStatusCode, response.StatusDescription);
                        completed.Set();
                        return;
                    }
                    try
                    {
                        result = response.Body.ParseJson<ClusterMessages.ClusterInfoDto>();
                        //_log.Debug("ClusterDnsEndPointDiscoverer: Got gossip from [{0}]:\n{1}.", endPoint, string.Join("\n", result.Members.Select(x => x.ToString())));
                    }
                    catch (Exception)
                    {
                        //_log.Info("Failed to get cluster info from [{0}]: deserialization error: {1}.", endPoint, e.Message);
                    }
                    completed.Set();
                },
                e =>
                {
                    //_log.Info("Failed to get cluster info from [{0}]: request failed, error: {1}.", endPoint, e.Message);
                    completed.Set();
                });

            completed.Wait();
            return result;
        }

        private NodeEndPoints? TryDetermineBestNode(IEnumerable<ClusterMessages.MemberInfoDto> members)
        {
            var notAllowedStates = new[]
            {
                ClusterMessages.VNodeState.Manager, 
                ClusterMessages.VNodeState.ShuttingDown,
                ClusterMessages.VNodeState.Shutdown
            };
            var node = members.Where(x => x.IsAlive)
                              .Where(x => !notAllowedStates.Contains(x.State))
                              .OrderByDescending(x => x.State)
                              .FirstOrDefault();
            if (node == null)
            {
                //_log.Info("Unable to locate suitable node. Gossip info:\n{0}.", string.Join("\n", members.Select(x => x.ToString())));
                return null;
            }

            var normTcp = new IPEndPoint(IPAddress.Parse(node.ExternalTcpIp), node.ExternalTcpPort);
            var secTcp = node.ExternalSecureTcpPort > 0
                                 ? new IPEndPoint(IPAddress.Parse(node.ExternalTcpIp), node.ExternalSecureTcpPort)
                                 : null;
            _log.Info("Discovering: found best choice [{0},{1}] ({2}).", normTcp, secTcp == null ? "n/a" : secTcp.ToString(), node.State);
            return new NodeEndPoints(normTcp, secTcp);
        }
    }
}
