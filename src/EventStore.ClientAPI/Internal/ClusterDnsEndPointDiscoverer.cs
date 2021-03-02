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
		private readonly DnsEndPoint _clusterDns;
		private readonly int _maxDiscoverAttempts;
		private readonly GossipSeed[] _gossipSeeds;

		private readonly IHttpClient _client;
		private Tuple<ClusterMessages.MemberInfoDto[], GossipSeed> _oldGossip;
		private readonly TimeSpan _gossipTimeout;

		private readonly NodePreference _nodePreference;
		private readonly ICompatibilityMode _compatibilityMode;

		public ClusterDnsEndPointDiscoverer(ILogger log,
			string clusterDns,
			int maxDiscoverAttempts,
			int httpGossipPort,
			GossipSeed[] gossipSeeds,
			TimeSpan gossipTimeout,
			NodePreference nodePreference,
			ICompatibilityMode compatibilityMode,
			IHttpClient httpAsyncClient) {
			Ensure.NotNull(log, "log");

			_log = log;
			_clusterDns = (gossipSeeds?.Length ?? 0) == 0
				? new DnsEndPoint(clusterDns, httpGossipPort)
				: null;
			_maxDiscoverAttempts = maxDiscoverAttempts;
			_gossipSeeds = gossipSeeds;
			_gossipTimeout = gossipTimeout;
			_compatibilityMode = compatibilityMode;
			_client = httpAsyncClient;
			_nodePreference = nodePreference;
		}

		public Task<NodeEndPoints> DiscoverAsync(EndPoint failedTcpEndPoint) {
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

				throw new ClusterException($"Failed to discover candidate in {_maxDiscoverAttempts} attempts.");
			});
		}

		private NodeEndPoints? DiscoverEndPoint(EndPoint failedEndPoint) {
			var oldGossip = Interlocked.Exchange(ref _oldGossip, null);
			var gossipCandidates = oldGossip != null
				? GetGossipCandidatesFromOldGossip(oldGossip.Item1, oldGossip.Item2, failedEndPoint)
				: GetGossipCandidatesFromConfig();
			for (int i = 0; i < gossipCandidates.Length; ++i) {
				var gossip = TryGetGossipFrom(gossipCandidates[i]);
				if (gossip == null || gossip.Members == null || gossip.Members.Length == 0)
					continue;

				var bestNode = TryDetermineBestNode(gossip.Members, _nodePreference);
				if (bestNode != null) {
					_oldGossip = Tuple.Create(gossip.Members, gossipCandidates[i]);
					return bestNode;
				}
			}

			return null;
		}

		private GossipSeed[] GetGossipCandidatesFromConfig() {
			//_log.Debug("ClusterDnsEndPointDiscoverer: GetGossipCandidatesFromDns");
			var endpoints = _gossipSeeds;

			if ((endpoints?.Length ?? 0) == 0) {
				if (_compatibilityMode.IsAutoCompatibilityModeEnabled()) {
					endpoints = new[] {
						new GossipSeed(_clusterDns, seedOverTls: true, v5HostHeader: false), // Try HTTPS gossip first (v20 defaults)
						new GossipSeed(_clusterDns, seedOverTls: false, v5HostHeader: true) // Then HTTP gossip (v5 defaults)
					};
				} else if (_compatibilityMode.IsVersion5CompatibilityModeEnabled()) {
					// if v5 compatibility mode is enabled, then use v5 defaults
					endpoints = new[] {
						new GossipSeed(_clusterDns, seedOverTls: false, v5HostHeader: true)
					};
				} else {
					// Use only v20 defaults 
					endpoints = new[] {
						new GossipSeed(_clusterDns, seedOverTls: true, v5HostHeader: false)
					};
				}
			} else {
				RandomShuffle(endpoints, 0, endpoints.Length - 1);
			}

			return endpoints;
		}

		private GossipSeed[] GetGossipCandidatesFromOldGossip(IEnumerable<ClusterMessages.MemberInfoDto> oldGossip,
			GossipSeed discoveredFrom,
			EndPoint failedTcpEndPoint) {
			var gossipCandidates = failedTcpEndPoint == null
				? oldGossip.ToArray()
				: oldGossip.Where(x => !(x.ExternalTcpPort == failedTcpEndPoint.GetPort()
										 && x.ExternalTcpIp.Equals(failedTcpEndPoint.GetHost())))
					.ToArray();
			return ArrangeGossipCandidates(gossipCandidates, discoveredFrom);
		}

		private GossipSeed[] ArrangeGossipCandidates(ClusterMessages.MemberInfoDto[] members, GossipSeed discoveredFrom) {
			// We assume that if the members are discovered via a non-TLS endpoint, the members will also be non-TLS.  Same for v5 header stuff.
			var result = new GossipSeed[members.Length];
			int i = -1;
			int j = members.Length;
			for (int k = 0; k < members.Length; ++k) {
				if (members[k].State == ClusterMessages.VNodeState.Manager)
					result[--j] = new GossipSeed(new DnsEndPoint(members[k].HttpAddress,
						members[k].HttpPort), discoveredFrom.SeedOverTls, discoveredFrom.V5HostHeader);
				else
					result[++i] = new GossipSeed(new DnsEndPoint(members[k].HttpAddress,
						members[k].HttpPort), discoveredFrom.SeedOverTls, discoveredFrom.V5HostHeader);
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

		private ClusterMessages.ClusterInfoDto TryGetGossipFrom(GossipSeed endPoint) {
			//_log.Debug("ClusterDnsEndPointDiscoverer: Trying to get gossip from [{0}].", endPoint);

			ClusterMessages.ClusterInfoDto result = null;
			var completed = new ManualResetEventSlim(false);

			var url = endPoint.EndPoint.ToHttpUrl(
				endPoint.SeedOverTls ? EndpointExtensions.HTTPS_SCHEMA : EndpointExtensions.HTTP_SCHEMA,
				"/gossip?format=json", true);
			_client.Get(
				url,
				null,
				response => {
					try {
						if (response.HttpStatusCode != HttpStatusCode.OK) {
							_log.Info("[{0}] responded with {1} ({2})", endPoint.EndPoint, response.HttpStatusCode,
								response.StatusDescription);
							return;
						}

						try {
							result = response.Body.ParseJson<ClusterMessages.ClusterInfoDto>();
							//_log.Debug("ClusterDnsEndPointDiscoverer: Got gossip from [{0}]:\n{1}.", endPoint, string.Join("\n", result.Members.Select(x => x.ToString())));
						} catch (Exception e) {
							if (e is AggregateException ae)
								e = ae.Flatten();
							_log.Error("Failed to get cluster info from [{0}]: deserialization error: {1}.", endPoint.EndPoint, e);
						}
					} finally {
						completed.Set();
					}
				},
				e => {
					try {
						if (e is AggregateException ae)
							e = ae.Flatten();
						_log.Error("Failed to get cluster info from [{0}]: request failed, error: {1}.", endPoint.EndPoint, e);
					} finally {
						completed.Set();
					}
				},
				// https://github.com/EventStore/EventStore/pull/2744#pullrequestreview-562358658
				endPoint.V5HostHeader ? "" : endPoint.EndPoint.GetHost());
			completed.Wait(_gossipTimeout);
			return result;
		}

		private NodeEndPoints? TryDetermineBestNode(IEnumerable<ClusterMessages.MemberInfoDto> members,
			NodePreference nodePreference) {
			var notAllowedStates = new[] {
				ClusterMessages.VNodeState.Manager,
				ClusterMessages.VNodeState.ShuttingDown,
				ClusterMessages.VNodeState.Manager,
				ClusterMessages.VNodeState.Shutdown,
				ClusterMessages.VNodeState.Unknown,
				ClusterMessages.VNodeState.Initializing,
				ClusterMessages.VNodeState.CatchingUp,
				ClusterMessages.VNodeState.ShuttingDown,
				ClusterMessages.VNodeState.PreLeader,
				ClusterMessages.VNodeState.PreReplica,
				ClusterMessages.VNodeState.PreReadOnlyReplica,
				ClusterMessages.VNodeState.Clone
			};

			var nodes = members.Where(x => x.IsAlive)
				.Where(x => !notAllowedStates.Contains(x.State))
				.OrderByDescending(x => x.State)
				.ToArray();

			switch (nodePreference) {
				case NodePreference.Random:
					RandomShuffle(nodes, 0, nodes.Length - 1);
					break;
				case NodePreference.Leader:
					nodes = nodes.OrderBy(nodeEntry => nodeEntry.State != ClusterMessages.VNodeState.Leader)
						.ToArray(); // OrderBy is a stable sort and only affects order of matching entries
					break;
				case NodePreference.Follower:
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

			var normTcp = new DnsEndPoint(node.ExternalTcpIp, node.ExternalTcpPort);
			var secTcp = node.ExternalSecureTcpPort > 0
				? new DnsEndPoint(node.ExternalTcpIp, node.ExternalSecureTcpPort)
				: null;
			_log.Info("Discovering: found best choice [{0},{1}] ({2}).", normTcp,
				secTcp == null ? "n/a" : secTcp.ToString(), node.State);
			return new NodeEndPoints(normTcp, secTcp);
		}

		private bool IsReadOnlyReplicaState(ClusterMessages.VNodeState state) {
			return state == ClusterMessages.VNodeState.ReadOnlyLeaderless
				   || state == ClusterMessages.VNodeState.ReadOnlyReplica;
		}
	}
}
