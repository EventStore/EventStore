using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Log;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using EventStore.Common.Utils;
using Xunit;
using HttpStatusCode = System.Net.HttpStatusCode;

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
				nodePreference,
				new NoCompatibilityMode(),
				new TestHttpClient(response.ToJson()));

			var result = await sut.DiscoverAsync(new IPEndPoint(IPAddress.Any, 1113));
			Assert.Equal(1113, EndPointExtensions.GetPort(result.TcpEndPoint));
		}
	}

	internal class TestHttpClient : IHttpClient {
		private readonly string _response;
		public TestHttpClient(string response) {
			_response = response;
		}

		public void Get(string url, UserCredentials userCredentials, Action<HttpResponse> onSuccess, Action<Exception> onException, string hostHeader = "") {
			var request = new HttpRequestMessage();
			request.RequestUri = new Uri(url);
			request.Method = new System.Net.Http.HttpMethod("GET");
			onSuccess(new HttpResponse(new HttpResponseMessage(HttpStatusCode.OK) {
				Content = new StringContent(_response),
				RequestMessage = request
			}) {
				Body = _response
			});
		}

		public void Post(string url, string body, string contentType, UserCredentials userCredentials, Action<HttpResponse> onSuccess,
			Action<Exception> onException) {
			throw new NotImplementedException();
		}

		public void Delete(string url, UserCredentials userCredentials, Action<HttpResponse> onSuccess, Action<Exception> onException) {
			throw new NotImplementedException();
		}

		public void Put(string url, string body, string contentType, UserCredentials userCredentials, Action<HttpResponse> onSuccess,
			Action<Exception> onException) {
			throw new NotImplementedException();
		}
	}
}
