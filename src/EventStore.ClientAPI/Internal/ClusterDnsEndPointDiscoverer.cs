using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.Transport.Http;
using System.Linq;
using System.Net.Http;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

namespace EventStore.ClientAPI.Internal {
	internal class ClusterDnsEndPointDiscoverer : IEndPointDiscoverer {
		private readonly ILogger _log;
		private readonly string _clusterDns;
		private readonly int _maxDiscoverAttempts;
		private readonly int _managerExternalHttpPort;
		private readonly GossipSeed[] _gossipSeeds;

		private readonly IHttpClient _client;
		private ClusterMessages.MemberInfoDto[] _oldGossip;
		private TimeSpan _gossipTimeout;

		private readonly NodePreference _nodePreference;
		private readonly ICompatibilityMode _compatibilityMode;

		public ClusterDnsEndPointDiscoverer(ILogger log,
			string clusterDns,
			int maxDiscoverAttempts,
			int managerExternalHttpPort,
			GossipSeed[] gossipSeeds,
			TimeSpan gossipTimeout,
			NodePreference nodePreference,
			ICompatibilityMode compatibilityMode,
			IHttpClient client,
			bool skipCertificateValidation) {
			Ensure.NotNull(log, "log");

			_log = log;
			_clusterDns = clusterDns;
			_maxDiscoverAttempts = maxDiscoverAttempts;
			_managerExternalHttpPort = managerExternalHttpPort;
			_gossipSeeds = gossipSeeds;
			_gossipTimeout = gossipTimeout;
			HttpClientHandler handler = null;
			if (skipCertificateValidation) {
#if NET452 || NET46
				handler = new WebRequestHandler {
					ServerCertificateValidationCallback = delegate { return true; }
				};
#else
				handler = new HttpClientHandler {
					ServerCertificateCustomValidationCallback = delegate { return true; }
				};
#endif
			}
			_compatibilityMode = compatibilityMode;
			_client = client ?? new HttpAsyncClient(_gossipTimeout, handler);
			_nodePreference = nodePreference;
		}

		public Task<NodeEndPoints> DiscoverAsync(IPEndPoint failedTcpEndPoint) {
			return Task.Factory.StartNew(() => {
				var maxDiscoverAttemptsStr = "";
				if (_maxDiscoverAttempts != Int32.MaxValue)
					maxDiscoverAttemptsStr = "/" + _maxDiscoverAttempts;

				for (int attempt = 1; attempt <= _maxDiscoverAttempts; ++attempt) {
					//_log.Info("Discovering cluster. Attempt {0}/{1}...", attempt, _maxDiscoverAttempts);
					try {
						var endPoints = DiscoverEndPoint(failedTcpEndPoint);
						if (endPoints != null) {
							_log.Info("Discovering attempt {0}{1} successful: best candidate is {2}.", attempt,
								maxDiscoverAttemptsStr, endPoints);
							return endPoints.Value;
						}

						_log.Info("Discovering attempt {0}{1} failed: no candidate found.", attempt,
							maxDiscoverAttemptsStr);
					} catch (Exception exc) {
						_log.Info("Discovering attempt {0}{1} failed with error: {2}.", attempt, maxDiscoverAttemptsStr,
							exc);
					}

					Thread.Sleep(500);
				}

				throw new ClusterException(string.Format("Failed to discover candidate in {0} attempts.",
					_maxDiscoverAttempts));
			});
		}

		private NodeEndPoints? DiscoverEndPoint(IPEndPoint failedEndPoint) {
			var oldGossip = Interlocked.Exchange(ref _oldGossip, null);
			var gossipCandidates = oldGossip != null
				? GetGossipCandidatesFromOldGossip(oldGossip, failedEndPoint)
				: GetGossipCandidatesFromConfig();
			for (int i = 0; i < gossipCandidates.Length; ++i) {
				var gossip = TryGetGossipFrom(gossipCandidates[i]);
				if (gossip == null || gossip.Members == null || gossip.Members.Length == 0)
					continue;

				var bestNode = TryDetermineBestNode(gossip.Members, _nodePreference, gossipCandidates[i].GetHostHeader());
				if (bestNode != null) {
					_oldGossip = gossip.Members;
					return bestNode;
				}
			}

			return null;
		}

		private IGossipSeed[] GetGossipCandidatesFromConfig() {
			//_log.Debug("ClusterDnsEndPointDiscoverer: GetGossipCandidatesFromDns");
			
			IGossipSeed[] endpoints;

			if ((_gossipSeeds?.Length ?? 0) != 0) {
				if (_compatibilityMode.IsAutoCompatibilityModeEnabled()) {
					var tmp = new List<IGossipSeed>();
					for (var i = 0; i < _gossipSeeds.Length; i++) {
						var current = _gossipSeeds[i];
						
						// We try v20 first
						tmp.Add(new GossipSeed(current.EndPoint, current.HostHeader, seedOverTls: true, v20Compatibility: true));
						// Then v5
						tmp.Add(current);
					}

					endpoints = tmp.ToArray();
				} else if (_compatibilityMode.IsVersion5CompatibilityModeEnabled()) {
					// Safe in this case.
					endpoints = _gossipSeeds;
					RandomShuffle(endpoints, 0, endpoints.Length - 1);
				} else {
					// Convert to v20.
					endpoints = new IGossipSeed[_gossipSeeds.Length];
					for (var i = 0; i < _gossipSeeds.Length; i++) {
						var current = _gossipSeeds[i];
						endpoints[i] = new GossipSeed(current.EndPoint, current.HostHeader, true,
							v20Compatibility: true);
					}
					
					RandomShuffle(endpoints, 0, endpoints.Length - 1);
				}
			} else {
				if (_compatibilityMode.IsAutoCompatibilityModeEnabled()) {
					endpoints = new[] {
						new ClusterDnsSeed(_clusterDns, _managerExternalHttpPort, seedOverTls: true, v20Compatibility: true), // Try HTTPS gossip first (v20 defaults)
						new ClusterDnsSeed(_clusterDns, _managerExternalHttpPort, seedOverTls: false, v20Compatibility: false), // Then HTTP gossip (v5 defaults)
					};
				} else if (_compatibilityMode.IsVersion5CompatibilityModeEnabled()) {
					// if v5 compatibility mode is enabled, then use v5 defaults
					endpoints = ResolveDns(_clusterDns)
						.Select(x => new GossipSeed(new IPEndPoint(x, _managerExternalHttpPort))).ToArray();
				} else {
					// Use only v20 defaults
					endpoints = new[] {
						new ClusterDnsSeed(_clusterDns, _managerExternalHttpPort, seedOverTls: true, v20Compatibility: true)
					};
				}
			}
			
			return endpoints;
		}

		private IPAddress[] ResolveDns(string dns) {
			IPAddress[] addresses;
			try {
				addresses = Dns.GetHostAddresses(dns);
			} catch (Exception exc) {
				throw new ClusterException(string.Format("Error while resolving DNS entry '{0}'.", _clusterDns), exc);
			}

			if (addresses == null || addresses.Length == 0)
				throw new ClusterException(string.Format("DNS entry '{0}' resolved into empty list.", _clusterDns));
			return addresses;
		}

		private IGossipSeed[] GetGossipCandidatesFromOldGossip(IEnumerable<ClusterMessages.MemberInfoDto> oldGossip,
			IPEndPoint failedTcpEndPoint) {
			//_log.Debug("ClusterDnsEndPointDiscoverer: GetGossipCandidatesFromOldGossip, failedTcpEndPoint: {0}.", failedTcpEndPoint);
			var gossipCandidates = failedTcpEndPoint == null
				? oldGossip.ToArray()
				: oldGossip.Where(x => !((x.ExternalTcpPort == failedTcpEndPoint.Port || x.ExternalSecureTcpPort == failedTcpEndPoint.Port)
				                         && Resolution.Resolve(x.ExternalTcpIp).Equals(failedTcpEndPoint.Address)))
					.ToArray();
			
			var result = ArrangeGossipCandidates(gossipCandidates);
			
			if (_compatibilityMode.IsAutoCompatibilityModeEnabled()) {
				var tmp = new List<IGossipSeed>();
				for (var i = 0; i < result.Length; i++) {
					var current = result[i];
						
					// We try v20 first
					tmp.Add(new GossipSeed(current.EndPoint, current.HostHeader, seedOverTls: true, v20Compatibility: true));
					// Then v5
					tmp.Add(current);
				}

				return tmp.ToArray();
			}

			// If version 5 compatibility mode is not enable, it means we need to convert
			// gossip seeds to v20 format.
			if (!_compatibilityMode.IsVersion5CompatibilityModeEnabled()) {
				for (var i = 0; i < result.Length; i++) {
					var current = result[i];
					result[i] = new GossipSeed(current.EndPoint, current.HostHeader, true,
						v20Compatibility: true);
				}
			}

			return result;
		}

		private GossipSeed[] ArrangeGossipCandidates(ClusterMessages.MemberInfoDto[] members) {
			var result = new GossipSeed[members.Length];
			int i = -1;
			int j = members.Length;
			for (int k = 0; k < members.Length; ++k) {
				if (members[k].State == ClusterMessages.VNodeState.Manager)
					result[--j] = new GossipSeed(new IPEndPoint(Resolution.Resolve(members[k].ExternalHttpIp),
						members[k].ExternalHttpPort));
				else
					result[++i] = new GossipSeed(new IPEndPoint(Resolution.Resolve(members[k].ExternalHttpIp),
						members[k].ExternalHttpPort));
			}

			RandomShuffle(result, 0, i); // shuffle nodes
			RandomShuffle(result, j, members.Length - 1); // shuffle managers

			return result;
		}

		private void RandomShuffle<T>(T[] arr, int i, int j) {
			if (i >= j)
				return;
			var rnd = new Random(Guid.NewGuid().GetHashCode());
			for (int k = i; k <= j; ++k) {
				var index = rnd.Next(k, j + 1);
				var tmp = arr[index];
				arr[index] = arr[k];
				arr[k] = tmp;
			}
		}

		private ClusterMessages.ClusterInfoDto TryGetGossipFrom(IGossipSeed endPoint) {
			//_log.Debug("ClusterDnsEndPointDiscoverer: Trying to get gossip from [{0}].", endPoint);

			ClusterMessages.ClusterInfoDto result = null;
			var completed = new ManualResetEventSlim(false);

			var url = endPoint.ToHttpUrl();
			_client.Get(
				url,
				null,
				response => {
					if (response.HttpStatusCode != HttpStatusCode.OK) {
						_log.Info("[{0}] responded with {1} ({2})", endPoint.ToHttpUrl(), response.HttpStatusCode, response.StatusDescription);
						completed.Set();
						return;
					}

					try {
						result = response.Body.ParseJson<ClusterMessages.ClusterInfoDto>();
						//_log.Debug("ClusterDnsEndPointDiscoverer: Got gossip from [{0}]:\n{1}.", endPoint, string.Join("\n", result.Members.Select(x => x.ToString())));
					} catch (Exception e) {
						if (e is AggregateException ae)
							e = ae.Flatten();
						_log.Error("Failed to get cluster info from [{0}]: deserialization error: {1}.", endPoint.ToHttpUrl(), e);
					}

					completed.Set();
				},
				e => {
					if (e is AggregateException ae)
						e = ae.Flatten();
					_log.Error("Failed to get cluster info from [{0}]: request failed, error: {1}.", endPoint.ToHttpUrl(), e);
					completed.Set();
				}, endPoint.GetHostHeader());

			if (!completed.Wait(_gossipTimeout)) {
				_log.Error("Timed out trying to get cluster info from [{0}].", endPoint.ToHttpUrl());
			}
			return result;
		}

		private NodeEndPoints? TryDetermineBestNode(IEnumerable<ClusterMessages.MemberInfoDto> members,
			NodePreference nodePreference, string host) {
			var notAllowedStates = new[] {
				ClusterMessages.VNodeState.Manager,
				ClusterMessages.VNodeState.ShuttingDown,
				ClusterMessages.VNodeState.Shutdown
			};

			var nodes = members.Where(x => x.IsAlive)
				.Where(x => !notAllowedStates.Contains(x.State))
				.OrderByDescending(x => x.State)
				.ToArray();

			switch (nodePreference) {
				case NodePreference.Random:
					RandomShuffle(nodes, 0, nodes.Length - 1);
					break;
				case NodePreference.Slave:
					nodes = nodes.OrderBy(nodeEntry =>
							nodeEntry.State != ClusterMessages.VNodeState.Follower &&
							 nodeEntry.State != ClusterMessages.VNodeState.Slave)
						.ToArray(); // OrderBy is a stable sort and only affects order of matching entries
					RandomShuffle(nodes, 0,
						nodes.Count(nodeEntry => nodeEntry.State == ClusterMessages.VNodeState.Follower ||
						                          nodeEntry.State == ClusterMessages.VNodeState.Slave) - 1);
					break;
				case NodePreference.ReadOnlyReplica:
					nodes = nodes.OrderBy(nodeEntry => !IsReadOnlyReplicaState(nodeEntry.State))
						.ToArray(); // OrderBy is a stable sort and only affects order of matching entries
					RandomShuffle(nodes, 0,
						nodes.Count(nodeEntry => IsReadOnlyReplicaState(nodeEntry.State)) - 1);
					break;
			}

			var node = nodes.FirstOrDefault();

			if (node == default(ClusterMessages.MemberInfoDto)) {
				//_log.Info("Unable to locate suitable node. Gossip info:\n{0}.", string.Join("\n", members.Select(x => x.ToString())));
				return null;
			}

			var nodeAddress = Resolution.Resolve(node.ExternalTcpIp);

			var normTcp = new IPEndPoint(nodeAddress, node.ExternalTcpPort);
			var secTcp = node.ExternalSecureTcpPort > 0
				? new IPEndPoint(nodeAddress, node.ExternalSecureTcpPort)
				: null;
			_log.Info("Discovering: found best choice [{0},{1}] ({2}).", normTcp,
				secTcp == null ? "n/a" : secTcp.ToString(), node.State);
			return new NodeEndPoints(normTcp, secTcp, host);
		}

		private bool IsReadOnlyReplicaState(ClusterMessages.VNodeState state) {
			return state == ClusterMessages.VNodeState.ReadOnlyLeaderless
			       || state == ClusterMessages.VNodeState.PreReadOnlyReplica
			       || state == ClusterMessages.VNodeState.ReadOnlyReplica;
		}
	}
}
