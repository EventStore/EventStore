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
		private readonly int _httpGossipPort;
		private readonly GossipSeed[] _gossipSeeds;

		private readonly IHttpClient _client;
		private ClusterMessages.MemberInfoDto[] _oldGossip;
		private TimeSpan _gossipTimeout;

		private readonly NodePreference _nodePreference;

		public ClusterDnsEndPointDiscoverer(ILogger log,
			string clusterDns,
			int maxDiscoverAttempts,
			int httpGossipPort,
			GossipSeed[] gossipSeeds,
			TimeSpan gossipTimeout,
			NodePreference nodePreference,
			HttpMessageHandler httpMessageHandler = null) {
			Ensure.NotNull(log, "log");

			_log = log;
			_clusterDns = clusterDns;
			_maxDiscoverAttempts = maxDiscoverAttempts;
			_httpGossipPort = httpGossipPort;
			_gossipSeeds = gossipSeeds;
			_gossipTimeout = gossipTimeout;
			_client = new HttpAsyncClient(_gossipTimeout, httpMessageHandler);
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

				throw new ClusterException(string.Format("Failed to discover candidate in {0} attempts.",
					_maxDiscoverAttempts));
			});
		}

		private NodeEndPoints? DiscoverEndPoint(EndPoint failedEndPoint) {
			var oldGossip = Interlocked.Exchange(ref _oldGossip, null);
			var gossipCandidates = oldGossip != null
				? GetGossipCandidatesFromOldGossip(oldGossip, failedEndPoint)
				: GetGossipCandidatesFromDns();
			for (int i = 0; i < gossipCandidates.Length; ++i) {
				var gossip = TryGetGossipFrom(gossipCandidates[i]);
				if (gossip == null || gossip.Members == null || gossip.Members.Length == 0)
					continue;

				var bestNode = TryDetermineBestNode(gossip.Members, _nodePreference);
				if (bestNode != null) {
					_oldGossip = gossip.Members;
					return bestNode;
				}
			}

			return null;
		}

		private GossipSeed[] GetGossipCandidatesFromDns() {
			//_log.Debug("ClusterDnsEndPointDiscoverer: GetGossipCandidatesFromDns");
			GossipSeed[] endpoints;
			if (_gossipSeeds != null && _gossipSeeds.Length > 0) {
				endpoints = _gossipSeeds;
			} else {
				endpoints = ResolveDns(_clusterDns)
					.Select(x => new GossipSeed(new IPEndPoint(x, _httpGossipPort))).ToArray();
			}

			RandomShuffle(endpoints, 0, endpoints.Length - 1);
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

		private GossipSeed[] GetGossipCandidatesFromOldGossip(IEnumerable<ClusterMessages.MemberInfoDto> oldGossip,
			EndPoint failedTcpEndPoint) {
			var gossipCandidates = failedTcpEndPoint == null
				? oldGossip.ToArray()
				: oldGossip.Where(x => !(x.ExternalTcpPort == failedTcpEndPoint.GetPort()
				                         && x.ExternalTcpIp.Equals(failedTcpEndPoint.GetHost())))
					.ToArray();
			return ArrangeGossipCandidates(gossipCandidates);
		}

		private GossipSeed[] ArrangeGossipCandidates(ClusterMessages.MemberInfoDto[] members) {
			var result = new GossipSeed[members.Length];
			int i = -1;
			int j = members.Length;
			for (int k = 0; k < members.Length; ++k) {
				if (members[k].State == ClusterMessages.VNodeState.Manager)
					result[--j] = new GossipSeed(new DnsEndPoint(members[k].HttpAddress,
						members[k].HttpPort));
				else
					result[++i] = new GossipSeed(new DnsEndPoint(members[k].HttpAddress,
						members[k].HttpPort));
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
					if (response.HttpStatusCode != HttpStatusCode.OK) {
						_log.Info("[{0}] responded with {1} ({2})", endPoint, response.HttpStatusCode,
							response.StatusDescription);
						completed.Set();
						return;
					}

					try {
						result = response.Body.ParseJson<ClusterMessages.ClusterInfoDto>();
						//_log.Debug("ClusterDnsEndPointDiscoverer: Got gossip from [{0}]:\n{1}.", endPoint, string.Join("\n", result.Members.Select(x => x.ToString())));
					} catch (Exception e) {
						if (e is AggregateException ae)
							e = ae.Flatten();
						_log.Error("Failed to get cluster info from [{0}]: deserialization error: {1}.", endPoint, e);
					}

					completed.Set();
				},
				e => {
					if (e is AggregateException ae)
						e = ae.Flatten();
					_log.Error("Failed to get cluster info from [{0}]: request failed, error: {1}.", endPoint, e);
					completed.Set();
				}, endPoint.EndPoint.GetHost());

			completed.Wait();
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
