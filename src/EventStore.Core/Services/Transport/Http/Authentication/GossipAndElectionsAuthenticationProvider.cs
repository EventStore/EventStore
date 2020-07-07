using EventStore.Core.Services.UserManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class GossipAndElectionsAuthenticationProvider : IHttpAuthenticationProvider {
		private readonly MediaTypeHeaderValue _gRPCHeader = new MediaTypeHeaderValue("application/grpc");
		private readonly PathString _gossipPath = new PathString("/event_store.cluster.Gossip");
		private readonly PathString _electionsPath = new PathString("/event_store.cluster.Elections");

		public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
			request = null;
			if (!(context.Request.GetTypedHeaders().ContentType?.IsSubsetOf(_gRPCHeader)).GetValueOrDefault(false))
				return false;

			if (!context.Request.Path.StartsWithSegments(_gossipPath) &&
			    !context.Request.Path.StartsWithSegments(_electionsPath))
				return false;

			request = new HttpAuthenticationRequest(context, "system", "");
			request.Authenticated(SystemAccounts.System);
			return true;
		}
	}
}
