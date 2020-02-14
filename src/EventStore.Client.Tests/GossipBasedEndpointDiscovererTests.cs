using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client {
	public class GossipBasedEndpointDiscovererTests {
		[Fact]
		public async Task should_issue_gossip_to_gossip_seed() {
			HttpRequestMessage request = null;
			var gossip = new ClusterMessages.ClusterInfo {
				Members = new[] {
					new ClusterMessages.MemberInfo {
						State = ClusterMessages.VNodeState.Leader,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 4444,
						IsAlive = true,
					},
				}
			};

			var handler = new FakeMessageHandler(req => {
				request = req;
				return ResponseFromGossip(gossip);
			});

			var gossipSeed = new DnsEndPoint("gossip_seed_endpoint", 1114);

			var sut = new ClusterEndpointDiscoverer(1, new[] {
				gossipSeed,
			}, Timeout.InfiniteTimeSpan, TimeSpan.Zero, NodePreference.Leader, handler);

			await sut.DiscoverAsync();

			Assert.Equal(Uri.UriSchemeHttps, request?.RequestUri.Scheme);
			Assert.Equal(gossipSeed.Host, request?.RequestUri.Host);
			Assert.Equal(gossipSeed.Port, request?.RequestUri.Port);
		}
		
		[Fact]
		public async Task should_be_able_to_discover_twice() {
			bool isFirstGossip = true;
			var firstGossip = new ClusterMessages.ClusterInfo {
				Members = new[] {
					new ClusterMessages.MemberInfo {
						State = ClusterMessages.VNodeState.Leader,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 1111,
						IsAlive = true,
					},
					new ClusterMessages.MemberInfo {
						State = ClusterMessages.VNodeState.Follower,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 2222,
						IsAlive = true,
					},
				}
			};
			var secondGossip = new ClusterMessages.ClusterInfo {
				Members = new[] {
					new ClusterMessages.MemberInfo {
						State = ClusterMessages.VNodeState.Leader,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 1111,
						IsAlive = false,
					},
					new ClusterMessages.MemberInfo {
						State = ClusterMessages.VNodeState.Leader,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 2222,
						IsAlive = true,
					},
				}
			};

			var handler = new FakeMessageHandler(req => {
				if (isFirstGossip) {
					isFirstGossip = false;
					return ResponseFromGossip(firstGossip);
				} else {
					return ResponseFromGossip(secondGossip);
				}
			});

			var gossipSeed = new DnsEndPoint("gossip_seed_endpoint", 1114);

			var sut = new ClusterEndpointDiscoverer(5, new[] {
				gossipSeed,
			}, Timeout.InfiniteTimeSpan, TimeSpan.Zero, NodePreference.Leader, handler);

			var result = await sut.DiscoverAsync();

			var expected = firstGossip.Members.First(x => x.ExternalHttpPort == 1111);
			
			Assert.Equal(expected.ExternalHttpIp, result.Address.ToString());
			Assert.Equal(expected.ExternalHttpPort, result.Port);
			
			result = await sut.DiscoverAsync();

			expected = secondGossip.Members.First(x => x.ExternalHttpPort == 2222);
			
			Assert.Equal(expected.ExternalHttpIp, result.Address.ToString());
			Assert.Equal(expected.ExternalHttpPort, result.Port);
		}

		[Fact]
		public async Task should_not_exceed_max_discovery_attempts() {
			int maxDiscoveryAttempts = 5;
			int discoveryAttempts = 0;

			var handler = new FakeMessageHandler(request => {
				discoveryAttempts++;
				throw new Exception();
			});

			var sut = new ClusterEndpointDiscoverer(maxDiscoveryAttempts, new[] {
				new DnsEndPoint("localhost", 1114),
			}, Timeout.InfiniteTimeSpan, TimeSpan.Zero, NodePreference.Leader, handler);

			await Assert.ThrowsAsync<DiscoveryException>(() => sut.DiscoverAsync());

			Assert.Equal(maxDiscoveryAttempts, discoveryAttempts);
		}

		[Theory,
		 InlineData(ClusterMessages.VNodeState.Manager),
		 InlineData(ClusterMessages.VNodeState.Shutdown),
		 InlineData(ClusterMessages.VNodeState.Unknown),
		 InlineData(ClusterMessages.VNodeState.Initializing),
		 InlineData(ClusterMessages.VNodeState.CatchingUp),
		 InlineData(ClusterMessages.VNodeState.ResigningLeader),
		 InlineData(ClusterMessages.VNodeState.ShuttingDown),
		 InlineData(ClusterMessages.VNodeState.PreLeader),
		 InlineData(ClusterMessages.VNodeState.PreReplica),
		 InlineData(ClusterMessages.VNodeState.PreReadOnlyReplica)]
		public async Task should_not_be_able_to_pick_invalid_node(ClusterMessages.VNodeState invalidState) {
			var gossip = new ClusterMessages.ClusterInfo {
				Members = new[] {
					new ClusterMessages.MemberInfo {
						State = invalidState,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 4444,
						IsAlive = true,
					},
				}
			};

			var handler = new FakeMessageHandler(req => ResponseFromGossip(gossip));

			var sut = new ClusterEndpointDiscoverer(1, new[] { new DnsEndPoint("localhost", 1113),
			}, Timeout.InfiniteTimeSpan, TimeSpan.Zero, NodePreference.Leader, handler);

			await Assert.ThrowsAsync<DiscoveryException>(() => sut.DiscoverAsync());
		}

		[Theory,
		 InlineData(NodePreference.Leader, ClusterMessages.VNodeState.Leader),
		 InlineData(NodePreference.Follower, ClusterMessages.VNodeState.Follower),
		 InlineData(NodePreference.ReadOnlyReplica, ClusterMessages.VNodeState.ReadOnlyReplica),
		 InlineData(NodePreference.ReadOnlyReplica, ClusterMessages.VNodeState.ReadOnlyLeaderless)]
		public async Task should_pick_node_based_on_preference(NodePreference preference,
			ClusterMessages.VNodeState expectedState) {
			var gossip = new ClusterMessages.ClusterInfo {
				Members = new[] {
					new ClusterMessages.MemberInfo {
						State = ClusterMessages.VNodeState.Leader,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 1111,
						IsAlive = true,
					},
					new ClusterMessages.MemberInfo {
						State = ClusterMessages.VNodeState.Follower,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 2222,
						IsAlive = true,
					},
					new ClusterMessages.MemberInfo {
						State = expectedState == ClusterMessages.VNodeState.ReadOnlyLeaderless
							? expectedState
							: ClusterMessages.VNodeState.ReadOnlyReplica,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 3333,
						IsAlive = true,
					},
					new ClusterMessages.MemberInfo {
						State = ClusterMessages.VNodeState.Manager,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 4444,
						IsAlive = true,
					},
				}
			};
			var handler = new FakeMessageHandler(req => ResponseFromGossip(gossip));

			var sut = new ClusterEndpointDiscoverer(1, new[] {
				new DnsEndPoint("localhost", 1113)
			}, Timeout.InfiniteTimeSpan, TimeSpan.Zero, preference, handler);

			var result = await sut.DiscoverAsync();
			Assert.Equal(result.Port,
				gossip.Members.Last(x => x.State == expectedState).ExternalHttpPort);
		}

		[Fact]
		public async Task falls_back_to_first_alive_node_if_a_preferred_node_is_not_found() {
			var gossip = new ClusterMessages.ClusterInfo {
				Members = new[] {
					new ClusterMessages.MemberInfo {
						State = ClusterMessages.VNodeState.Leader,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 1111,
						IsAlive = false,
					},
					new ClusterMessages.MemberInfo {
						State = ClusterMessages.VNodeState.Follower,
						InstanceId = Guid.NewGuid(),
						ExternalHttpIp = IPAddress.Any.ToString(),
						ExternalHttpPort = 2222,
						IsAlive = true,
					},
				}
			};
			var handler = new FakeMessageHandler(req => ResponseFromGossip(gossip));

			var sut = new ClusterEndpointDiscoverer(1, new[] {
				new DnsEndPoint("localhost", 1113)
			}, Timeout.InfiniteTimeSpan, TimeSpan.Zero, NodePreference.Leader, handler);

			var result = await sut.DiscoverAsync();
			Assert.Equal(result.Port,
				gossip.Members.Last(x => x.State == ClusterMessages.VNodeState.Follower).ExternalHttpPort);
		}

		private HttpResponseMessage ResponseFromGossip(ClusterMessages.ClusterInfo gossip) =>
			new HttpResponseMessage(HttpStatusCode.OK) {
				Content = new StringContent(JsonSerializer.Serialize(gossip, new JsonSerializerOptions {
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
					PropertyNameCaseInsensitive = true,
					Converters = {new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}
				}))
			};

		private class FakeMessageHandler : HttpMessageHandler {
			private readonly Func<HttpRequestMessage, HttpResponseMessage> _handle;

			public FakeMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) {
				_handle = handle;
			}

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
				CancellationToken cancellationToken) {
				return Task.FromResult(_handle(request));
			}
		}
	}
}
