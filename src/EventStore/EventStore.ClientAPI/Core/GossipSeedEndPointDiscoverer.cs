﻿//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net;
//using System.Threading;
//using System.Threading.Tasks;
//using EventStore.ClientAPI.Common.Utils;
//using EventStore.ClientAPI.Exceptions;
//using EventStore.ClientAPI.Messages;
//using EventStore.ClientAPI.Transport.Http;
//using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

//namespace EventStore.ClientAPI.Core
//{
//    internal class GossipSeedEndPointDiscoverer : IEndPointDiscoverer
//    {
//        private readonly ILogger _log;
//        private readonly IPEndPoint[] _gossipSeeds;
//        private readonly int _maxDiscoverAttempts;

//        private readonly HttpAsyncClient _client;
//        private ClusterMessages.MemberInfoDto[] _oldGossip;
//        private TimeSpan _gossipTimeout;

//        public GossipSeedEndPointDiscoverer(ILogger log,
//                                            IPEndPoint[] gossipSeeds,
//                                            TimeSpan gossipTimeout,
//                                            int maxDiscoverAttempts)
//        {
//            Ensure.NotNull(log, "log");

//            _log = log;
//            _maxDiscoverAttempts = maxDiscoverAttempts;
//            _gossipSeeds = gossipSeeds;
//            _gossipTimeout = gossipTimeout;
//            _client = new HttpAsyncClient(log);
//        }

//        public Task<NodeEndPoints> DiscoverAsync(IPEndPoint failedTcpEndPoint)
//        {
//            return Task.Factory.StartNew(() =>
//            {
//                for (int attempt = 1; attempt <= _maxDiscoverAttempts; ++attempt)
//                {
//                    //_log.Info("Discovering cluster. Attempt {0}/{1}...", attempt, _maxDiscoverAttempts);
//                    try
//                    {
//                        var endPoints = DiscoverEndPoint(failedTcpEndPoint);
//                        if (endPoints != null)
//                        {
//                            _log.Info("Discovering attempt {0}/{1} successful: best candidate is {2}.", attempt, _maxDiscoverAttempts, endPoints);
//                            return endPoints.Value;
//                        }

//                        _log.Info("Discovering attempt {0}/{1} failed: no candidate found.", attempt, _maxDiscoverAttempts);
//                    }
//                    catch (Exception exc)
//                    {
//                        _log.Info("Discovering attempt {0}/{1} failed with error: {2}.", attempt, _maxDiscoverAttempts, exc);
//                    }

//                    Thread.Sleep(500);
//                }
//                throw new ClusterException(string.Format("Failed to discover candidate in {0} attempts.", _maxDiscoverAttempts));
//            });
//        }

//        private NodeEndPoints? DiscoverEndPoint(IPEndPoint failedEndPoint)
//        {
//            var oldGossip = Interlocked.Exchange(ref _oldGossip, null);
//            var gossipCandidates = oldGossip != null
//                                           ? GetGossipCandidatesFromOldGossip(oldGossip, failedEndPoint)
//                                           : GetGossipCandidatesFromDns();
//            for (int i = 0; i < gossipCandidates.Length; ++i)
//            {
//                var gossip = TryGetGossipFrom(gossipCandidates[i]);
//                if (gossip == null || gossip.Members == null || gossip.Members.Length == 0)
//                    continue;

//                var bestNode = TryDetermineBestNode(gossip.Members);
//                if (bestNode != null)
//                {
//                    _oldGossip = gossip.Members;
//                    return bestNode;
//                }
//            }

//            return null;
//        }

//        private IPEndPoint[] GetGossipCandidatesFromDns()
//        {
//            //_log.Debug("ClusterDnsEndPointDiscoverer: GetGossipCandidatesFromDns");
//            IPEndPoint[] endpoints = _gossipSeeds;
//            RandomShuffle(endpoints, 0, endpoints.Length - 1);
//            return endpoints;
//        }


//        private IPEndPoint[] GetGossipCandidatesFromOldGossip(IEnumerable<ClusterMessages.MemberInfoDto> oldGossip, IPEndPoint failedTcpEndPoint)
//        {
//            //_log.Debug("ClusterDnsEndPointDiscoverer: GetGossipCandidatesFromOldGossip, failedTcpEndPoint: {0}.", failedTcpEndPoint);
//            var gossipCandidates = failedTcpEndPoint == null
//                    ? oldGossip.ToArray()
//                    : oldGossip.Where(x => !(x.ExternalTcpPort == failedTcpEndPoint.Port
//                                             && IPAddress.Parse(x.ExternalTcpIp).Equals(failedTcpEndPoint.Address)))
//                               .ToArray();
//            return ArrangeGossipCandidates(gossipCandidates);
//        }

//        private IPEndPoint[] ArrangeGossipCandidates(ClusterMessages.MemberInfoDto[] members)
//        {
//            var result = new IPEndPoint[members.Length];
//            int i = -1;
//            int j = members.Length;
//            for (int k = 0; k < members.Length; ++k)
//            {
//                if (members[k].State == ClusterMessages.VNodeState.Manager)
//                    result[--j] = new IPEndPoint(IPAddress.Parse(members[k].ExternalHttpIp), members[k].ExternalHttpPort);
//                else
//                    result[++i] = new IPEndPoint(IPAddress.Parse(members[k].ExternalHttpIp), members[k].ExternalHttpPort);
//            }
//            RandomShuffle(result, 0, i); // shuffle nodes
//            RandomShuffle(result, j, members.Length - 1); // shuffle managers

//            return result;
//        }

//        private void RandomShuffle<T>(T[] arr, int i, int j)
//        {
//            if (i >= j)
//                return;
//            var rnd = new Random(Guid.NewGuid().GetHashCode());
//            for (int k = i; k <= j; ++k)
//            {
//                var index = rnd.Next(k, j + 1);
//                var tmp = arr[index];
//                arr[index] = arr[k];
//                arr[k] = tmp;
//            }
//        }

//        private ClusterMessages.ClusterInfoDto TryGetGossipFrom(IPEndPoint endPoint)
//        {
//            //_log.Debug("ClusterDnsEndPointDiscoverer: Trying to get gossip from [{0}].", endPoint);

//            ClusterMessages.ClusterInfoDto result = null;
//            var completed = new ManualResetEventSlim(false);

//            var url = endPoint.ToHttpUrl("/gossip?format=json");
//            _client.Get(
//                url,
//                null,
//                _gossipTimeout,
//                response =>
//                {
//                    if (response.HttpStatusCode != HttpStatusCode.OK)
//                    {
//                        //_log.Info("[{0}] responded with {1} ({2})", endPoint, response.HttpStatusCode, response.StatusDescription);
//                        completed.Set();
//                        return;
//                    }
//                    try
//                    {
//                        result = response.Body.ParseJson<ClusterMessages.ClusterInfoDto>();
//                        //_log.Debug("ClusterDnsEndPointDiscoverer: Got gossip from [{0}]:\n{1}.", endPoint, string.Join("\n", result.Members.Select(x => x.ToString())));
//                    }
//                    catch (Exception)
//                    {
//                        //_log.Info("Failed to get cluster info from [{0}]: deserialization error: {1}.", endPoint, e.Message);
//                    }
//                    completed.Set();
//                },
//                e =>
//                {
//                    //_log.Info("Failed to get cluster info from [{0}]: request failed, error: {1}.", endPoint, e.Message);
//                    completed.Set();
//                });

//            completed.Wait();
//            return result;
//        }

//        private NodeEndPoints? TryDetermineBestNode(IEnumerable<ClusterMessages.MemberInfoDto> members)
//        {
//            var notAllowedStates = new[]
//            {
//                ClusterMessages.VNodeState.Manager, 
//                ClusterMessages.VNodeState.ShuttingDown,
//                ClusterMessages.VNodeState.Shutdown
//            };
//            var node = members.Where(x => x.IsAlive)
//                              .Where(x => !notAllowedStates.Contains(x.State))
//                              .OrderByDescending(x => x.State)
//                              .FirstOrDefault();
//            if (node == null)
//            {
//                //_log.Info("Unable to locate suitable node. Gossip info:\n{0}.", string.Join("\n", members.Select(x => x.ToString())));
//                return null;
//            }

//            var normTcp = new IPEndPoint(IPAddress.Parse(node.ExternalTcpIp), node.ExternalTcpPort);
//            var secTcp = node.ExternalSecureTcpPort > 0
//                                 ? new IPEndPoint(IPAddress.Parse(node.ExternalTcpIp), node.ExternalSecureTcpPort)
//                                 : null;
//            _log.Info("Discovering: found best choice [{0},{1}] ({2}).", normTcp, secTcp == null ? "n/a" : secTcp.ToString(), node.State);
//            return new NodeEndPoints(normTcp, secTcp);
//        }
//    }
//}