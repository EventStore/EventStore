using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.Messages;
using EventStore.Common.Utils;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class endpoint_discoverer {
		[Theory]
		[InlineData(ClusterMessages.VNodeState.Follower, NodePreference.Follower)]
		[InlineData(ClusterMessages.VNodeState.Slave, NodePreference.Follower)]
		internal async Task should_handle_node_preference_from_gossip(
			ClusterMessages.VNodeState nodeState,
			NodePreference nodePreference) {
			var response = new ClusterMessages.ClusterInfoDto {
				Members = new[] {
					new ClusterMessages.MemberInfoDto {
						State = nodeState,
						IsAlive = true,
						ExternalTcpIp = "127.0.0.1",
						ExternalTcpPort = 1113
					},
					new ClusterMessages.MemberInfoDto {
						State = ClusterMessages.VNodeState.Unknown,
						IsAlive = true,
						ExternalTcpIp = "127.0.0.1",
						ExternalTcpPort = 2113
					}
				}
			};

			var sut = new ClusterDnsEndPointDiscoverer(new ConsoleLogger(), "dns", 1, 1111,
				new[] {new GossipSeed(new IPEndPoint(IPAddress.Any, 2113))}, TimeSpan.FromSeconds(1),
				nodePreference, false,
				new TestMessageHandler(response.ToJson()));

			var result = await sut.DiscoverAsync(new IPEndPoint(IPAddress.Any, 1113));
			Assert.Equal(1113, EndPointExtensions.GetPort(result.TcpEndPoint));
		}
	}

	internal class TestMessageHandler : HttpMessageHandler {
		private readonly string _response;

		public TestMessageHandler(string response) {
			_response = response;
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken) {
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
				Content = new StringContent(_response),
				RequestMessage = request
			});
		}
	}
}
