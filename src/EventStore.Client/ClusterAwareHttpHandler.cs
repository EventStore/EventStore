using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Client {
	public class ClusterAwareHttpHandler : DelegatingHandler {
		private readonly bool _requiresLeader;
		private readonly IEndpointDiscoverer _endpointDiscoverer;
		private Lazy<Task<IPEndPoint>> _endpoint;

		public ClusterAwareHttpHandler(bool requiresLeader, IEndpointDiscoverer endpointDiscoverer) {
			_requiresLeader = requiresLeader;
			_endpointDiscoverer = endpointDiscoverer;
			_endpoint = new Lazy<Task<IPEndPoint>>(endpointDiscoverer.DiscoverAsync,
				LazyThreadSafetyMode.ExecutionAndPublication);
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken) {
			var endpointResolver = _endpoint;
			try {
				var endpoint = await endpointResolver.Value;

				request.RequestUri = new UriBuilder(Uri.UriSchemeHttps, endpoint.Address.ToString(), endpoint.Port,
					request.RequestUri.PathAndQuery).Uri;
				request.Headers.Add(Constants.Headers.RequiresLeader, _requiresLeader.ToString());
				return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
			} catch (Exception) {
				Interlocked.CompareExchange(ref _endpoint,
					new Lazy<Task<IPEndPoint>>(() => _endpointDiscoverer.DiscoverAsync(),
						LazyThreadSafetyMode.ExecutionAndPublication), endpointResolver);

				throw;
			}
		}

		public void ExceptionOccurred(Exception exception) {
			if (exception is NotLeaderException ex) {
				_endpoint = new Lazy<Task<IPEndPoint>>(Task.FromResult(ex.LeaderEndpoint));
			}
		}
	}
}
