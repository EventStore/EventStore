using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client {
	public class ClusterAwareHttpHandlerTests {
		[Theory, InlineData(true), InlineData(false)]
		public async Task should_set_requires_leader_header(bool requiresLeader) {
			var sut = new ClusterAwareHttpHandler(
				requiresLeader, new FakeEndpointDiscoverer(() => new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2113))) {
				InnerHandler = new TestMessageHandler()
			};

			var client = new HttpClient(sut);

			var request = new HttpRequestMessage(HttpMethod.Get, new UriBuilder().Uri);

			await client.SendAsync(request);

			request.Headers.TryGetValues(Constants.Headers.RequiresLeader, out var value);
			Assert.True(request.Headers.Contains(Constants.Headers.RequiresLeader));
			Assert.True(bool.Parse(value.First()) == requiresLeader);
		}

		[Fact]
		public async Task should_issue_request_to_discovered_endpoint() {
			var discoveredEndpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2113);

			var sut = new ClusterAwareHttpHandler(
				true, new FakeEndpointDiscoverer(() => discoveredEndpoint)) {
				InnerHandler = new TestMessageHandler()
			};

			var client = new HttpClient(sut);

			await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new UriBuilder().Uri));

			var request = new HttpRequestMessage(HttpMethod.Get, new UriBuilder().Uri);
			await client.SendAsync(request);

			Assert.Equal(discoveredEndpoint.Address.ToString(), request.RequestUri.Host);
			Assert.Equal(discoveredEndpoint.Port, request.RequestUri.Port);
		}

		[Fact]
		public async Task should_attempt_endpoint_discovery_on_next_request_when_request_fails() {
			int discoveryAttempts = 0;

			var sut = new ClusterAwareHttpHandler(
				true, new FakeEndpointDiscoverer(() => {
					discoveryAttempts++;
					throw new Exception();
				})) {
				InnerHandler = new TestMessageHandler()
			};

			var client = new HttpClient(sut);

			await Assert.ThrowsAsync<Exception>(() =>
				client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new UriBuilder().Uri)));
			await Assert.ThrowsAsync<Exception>(() =>
				client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new UriBuilder().Uri)));

			Assert.Equal(2, discoveryAttempts);
		}

		[Fact]
		public async Task should_set_endpoint_to_leader_endpoint_on_exception() {
			var sut = new ClusterAwareHttpHandler(
				true, new FakeEndpointDiscoverer(() => new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2113))) {
				InnerHandler = new TestMessageHandler()
			};

			var client = new HttpClient(sut);

			var request = new HttpRequestMessage(HttpMethod.Get, new UriBuilder().Uri);

			var newLeaderEndpoint = new IPEndPoint(IPAddress.Loopback, 1115);
			sut.ExceptionOccurred(new NotLeaderException(newLeaderEndpoint));
			await client.SendAsync(request);
			
			Assert.Equal(newLeaderEndpoint.Address.ToString(), request.RequestUri.Host);
			Assert.Equal(newLeaderEndpoint.Port, request.RequestUri.Port);
		}
	}

	internal class FakeEndpointDiscoverer : IEndpointDiscoverer {
		private readonly Func<IPEndPoint> _function;

		public FakeEndpointDiscoverer(Func<IPEndPoint> function) {
			_function = function;
		}

		public Task<IPEndPoint> DiscoverAsync() {
			return Task.FromResult(_function());
		}
	}

	internal class TestMessageHandler : HttpMessageHandler {
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken) {
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
		}
	}
}
