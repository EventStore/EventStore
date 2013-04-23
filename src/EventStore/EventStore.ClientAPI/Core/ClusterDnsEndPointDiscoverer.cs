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
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
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

        private readonly HttpAsyncClient _client;

        public ClusterDnsEndPointDiscoverer(ILogger log, string clusterDns, int maxDiscoverAttempts, int managerExternalHttpPort)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");

            _log = log;
            _clusterDns = clusterDns;
            _maxDiscoverAttempts = maxDiscoverAttempts;
            _managerExternalHttpPort = managerExternalHttpPort;

            _client = new HttpAsyncClient(log);
        }

        public Task<IPEndPoint> DiscoverAsync()
        {
            return Task<IPAddress[]>
                .Factory
                .FromAsync(Dns.BeginGetHostAddresses, Dns.EndGetHostAddresses, _clusterDns, null)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        throw new ClusterException(string.Format("Error while resolving DNS entry '{0}'.", _clusterDns), t.Exception);
                    if (t.Result == null || t.Result.Length == 0)
                        throw new ClusterException(string.Format("DNS entry '{0} resolved into empty list.", _clusterDns));
                    return FindBestClusterNode(t.Result, _maxDiscoverAttempts);
                });
        }

        private IPEndPoint FindBestClusterNode(IPAddress[] managers, int maxAttempts)
        {
            var info = RetrieveClusterGossip(managers, maxAttempts);
            if (info != null && info.Members != null && info.Members.Any())
            {
                var alive = info.Members.Where(m => m.IsAlive).ToArray();
                /*
                                if (!_allowForwarding)
                                {
                                    _log.Info("Forwarding denied. Looking for master...");
                                    var master = alive.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.Master);
                                    if (master == null)
                                    {
                                        _log.Info("Master not found");
                                        return null;
                                    }
                                    _log.Info("Master found on [{0}:{1}]", master.ExternalTcpIp, master.ExternalTcpPort);
                                    return new IPEndPoint(IPAddress.Parse(master.ExternalTcpIp), master.ExternalTcpPort);
                                }
                */

                var node = alive.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.Master) ??
                           alive.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.Slave) ??
                           alive.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.Clone) ??
                           alive.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.CatchingUp) ??
                           alive.FirstOrDefault(m => m.State == ClusterMessages.VNodeState.PreReplica);
                if (node != null)
                {
                    _log.Info("Best choice found, it's {0} on [{1}:{2}]", node.State, node.ExternalTcpIp, node.ExternalTcpPort);
                    return new IPEndPoint(IPAddress.Parse(node.ExternalTcpIp), node.ExternalTcpPort);
                }

                _log.Info("Unable to locate master, slave or clone node");
            }
            else
                _log.Info("Failed to discover cluster. No information available");

            throw new ClusterException(
                string.Format("Failed to discover cluster from DNS entry '{0}'.", _clusterDns));
        }

        private ClusterMessages.ClusterInfoDto RetrieveClusterGossip(IPAddress[] managers, int maxAttempts)
        {
            var attempt = 0;
            var random = new Random();
            while (attempt < maxAttempts)
            {
                _log.Info("Discovering cluster. Attempt {0} of {1}...", attempt + 1, maxAttempts);
                var i = random.Next(0, managers.Length);
                _log.Info("Picked [{0}]", managers[i]);
                var info = DownloadClusterGossip(new IPEndPoint(managers[i], _managerExternalHttpPort));
                if (info != null)
                {
                    _log.Info("Going to select node based on info from [{0}]", managers[i]);
                    return info;
                }
                _log.Info("Failed to get cluster info from [{0}].", managers[i]);
                attempt++;

                Thread.Sleep(TimeSpan.FromMilliseconds(300));
            }
            return null;
        }

        private ClusterMessages.ClusterInfoDto DownloadClusterGossip(IPEndPoint manager)
        {
            ClusterMessages.ClusterInfoDto result = null;
            var completed = new ManualResetEventSlim(false);

            var url = manager.ToHttpUrl("/gossip?format=xml");
            _log.Info("Sending gossip request to {0}...", url);
            _client.Get(
                url,
                response =>
                {
                    _log.Info("Got response from manager on [{0}]", manager);
                    if (response.HttpStatusCode != HttpStatusCode.OK)
                    {
                        _log.Info("Manager responded with {0} ({1})", response.HttpStatusCode, response.StatusDescription);
                        completed.Set();
                        return;
                    }
                    try
                    {
                        using (var reader = new StringReader(response.Body))
                        {
                            result = (ClusterMessages.ClusterInfoDto)new XmlSerializer(typeof(ClusterMessages.ClusterInfoDto)).Deserialize(reader);
                        }
                    }
                    catch (Exception e)
                    {
                        _log.Info(e, "Failed to get cluster info from manager on [{0}]. Deserialization error.", manager);
                    }
                    completed.Set();
                },
                e =>
                {
                    _log.Info(e, "Failed to get cluster info from manager on [{0}]. Request failed", manager);
                    completed.Set();
                });

            completed.Wait();
            return result;
        }
    }
}
