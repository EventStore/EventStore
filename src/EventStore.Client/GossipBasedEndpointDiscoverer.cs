using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Client {
	internal class ClusterEndpointDiscoverer : IEndpointDiscoverer {
		private readonly int _maxDiscoverAttempts;
		private readonly EndPoint[] _gossipSeeds;
		private readonly TimeSpan _discoveryInterval;

		private readonly HttpClient _client;
		private ClusterMessages.MemberInfo[] _oldGossip;

		private readonly NodePreference _nodePreference;

		private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions {
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = {new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}
		};

		public ClusterEndpointDiscoverer(
			int maxDiscoverAttempts,
			EndPoint[] gossipSeeds,
			TimeSpan gossipTimeout,
			TimeSpan discoveryInterval,
			NodePreference nodePreference,
			HttpMessageHandler httpMessageHandler = null) {
			_maxDiscoverAttempts = maxDiscoverAttempts;
			_gossipSeeds = gossipSeeds;
			_discoveryInterval = discoveryInterval;
			_client = new HttpClient(httpMessageHandler ?? new HttpClientHandler()) {Timeout = gossipTimeout};
			_nodePreference = nodePreference;
		}

		public async Task<IPEndPoint> DiscoverAsync() {
			for (int attempt = 1; attempt <= _maxDiscoverAttempts; ++attempt) {
				try {
					var endpoint = await DiscoverEndpointAsync().ConfigureAwait(false);
					if (endpoint != null) {
						return endpoint;
					}
				} catch (Exception exc) {
				}

				await Task.Delay(_discoveryInterval).ConfigureAwait(false);
			}

			throw new DiscoveryException($"Failed to discover candidate in {_maxDiscoverAttempts} attempts.");
		}

		private async Task<IPEndPoint> DiscoverEndpointAsync() {
			var oldGossip = Interlocked.Exchange(ref _oldGossip, null);
			var gossipCandidates = oldGossip != null
				? ArrangeGossipCandidates(oldGossip.ToArray())
				: GetGossipCandidates();
			foreach (var candidate in gossipCandidates) {
				var gossip = await TryGetGossipFrom(candidate).ConfigureAwait(false);
				if (gossip?.Members == null || gossip.Members.Length == 0)
					continue;

				var bestNode = TryDetermineBestNode(gossip.Members, _nodePreference);
				if (bestNode == null) continue;
				_oldGossip = gossip.Members;
				return bestNode;
			}

			return null;
		}

		private EndPoint[] GetGossipCandidates() {
			EndPoint[] endpoints = _gossipSeeds;
			RandomShuffle(endpoints, 0, endpoints.Length - 1);
			return endpoints;
		}

		private EndPoint[] ArrangeGossipCandidates(ClusterMessages.MemberInfo[] members) {
			var result = new EndPoint[members.Length];
			int i = -1;
			int j = members.Length;
			for (int k = 0; k < members.Length; ++k) {
				if (members[k].State == ClusterMessages.VNodeState.Manager)
					result[--j] = new IPEndPoint(IPAddress.Parse(members[k].ExternalHttpIp),
						members[k].ExternalHttpPort);
				else
					result[++i] = new IPEndPoint(IPAddress.Parse(members[k].ExternalHttpIp),
						members[k].ExternalHttpPort);
			}

			RandomShuffle(result, 0, i);
			RandomShuffle(result, j, members.Length - 1);

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

		private async Task<ClusterMessages.ClusterInfo> TryGetGossipFrom(EndPoint gossipSeed) {
			string url = string.Empty;
			switch (gossipSeed) {
				case DnsEndPoint dnsEndPoint:
					url = $"{Uri.UriSchemeHttps}://{dnsEndPoint.Host}:{dnsEndPoint.Port}/gossip?format=json";
					break;
				case IPEndPoint ipEndPoint:
					url = $"{Uri.UriSchemeHttps}://{ipEndPoint.Address}:{ipEndPoint.Port}/gossip?format=json";
					break;
			}

			var response = await _client.GetStringAsync(url).ConfigureAwait(false);
			var result = JsonSerializer.Deserialize<ClusterMessages.ClusterInfo>(response, _jsonSerializerOptions);
			return result;
		}

		private IPEndPoint TryDetermineBestNode(IEnumerable<ClusterMessages.MemberInfo> members,
			NodePreference nodePreference) {
			var notAllowedStates = new[] {
				ClusterMessages.VNodeState.Manager,
				ClusterMessages.VNodeState.ShuttingDown,
				ClusterMessages.VNodeState.Manager,
				ClusterMessages.VNodeState.Shutdown,
				ClusterMessages.VNodeState.Unknown,
				ClusterMessages.VNodeState.Initializing,
				ClusterMessages.VNodeState.CatchingUp,
				ClusterMessages.VNodeState.ResigningLeader,
				ClusterMessages.VNodeState.ShuttingDown,
				ClusterMessages.VNodeState.PreLeader,
				ClusterMessages.VNodeState.PreReplica,
				ClusterMessages.VNodeState.PreReadOnlyReplica,
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
						.ToArray();
					RandomShuffle(nodes, 0,
						nodes.Count(nodeEntry => nodeEntry.State == ClusterMessages.VNodeState.Leader) - 1);
					break;
				case NodePreference.Follower:
					nodes = nodes.OrderBy(nodeEntry => nodeEntry.State != ClusterMessages.VNodeState.Follower)
						.ToArray();
					RandomShuffle(nodes, 0,
						nodes.Count(nodeEntry => nodeEntry.State == ClusterMessages.VNodeState.Follower) - 1);
					break;
				case NodePreference.ReadOnlyReplica:
					nodes = nodes.OrderBy(nodeEntry => !IsReadOnlyReplicaState(nodeEntry.State))
						.ToArray();
					RandomShuffle(nodes, 0,
						nodes.Count(nodeEntry => IsReadOnlyReplicaState(nodeEntry.State)) - 1);
					break;
			}

			var node = nodes.FirstOrDefault();

			if (node == default(ClusterMessages.MemberInfo)) {
				return null;
			}

			return new IPEndPoint(IPAddress.Parse(node.ExternalHttpIp), node.ExternalHttpPort);
		}

		private bool IsReadOnlyReplicaState(ClusterMessages.VNodeState state) {
			return state == ClusterMessages.VNodeState.ReadOnlyLeaderless
			       || state == ClusterMessages.VNodeState.ReadOnlyReplica;
		}
	}
}
