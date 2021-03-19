using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Routing;

namespace EventStore.Core.Authentication.DelegatedAuthentication {
	public class DelegatedAuthenticationProvider : IAuthenticationProvider {
		public IAuthenticationProvider Inner { get; }

		public Task Initialize() => Inner.Initialize();

		public void Authenticate(AuthenticationRequest authenticationRequest) =>
			Inner.Authenticate(new DelegatedAuthenticationRequest(authenticationRequest));

		public IEnumerable<KeyValuePair<string, string>> GetPublicProperties() => Inner.GetPublicProperties();

		public void ConfigureEndpoints(IEndpointRouteBuilder endpointRouteBuilder) =>
			Inner.ConfigureEndpoints(endpointRouteBuilder);

		public IReadOnlyList<string> GetSupportedAuthenticationSchemes() => Inner.GetSupportedAuthenticationSchemes();

		public string Name => Inner.Name;

		public DelegatedAuthenticationProvider(IAuthenticationProvider inner) {
			Inner = inner;
		}

		private class DelegatedAuthenticationRequest : AuthenticationRequest {
			private readonly AuthenticationRequest _inner;

			public DelegatedAuthenticationRequest(AuthenticationRequest inner)
				: base(inner.Id, inner.Tokens) {
				_inner = inner;
			}

			public override void Unauthorized() => _inner.Unauthorized();

			public override void Authenticated(ClaimsPrincipal principal) {
				if (!principal.Identities.Any(identity => identity is DelegatedClaimsIdentity)) {
					principal.AddIdentity(new DelegatedClaimsIdentity(_inner.Tokens));
				}
				_inner.Authenticated(principal);
			}

			public override void Error() => _inner.Error();

			public override void NotReady() => _inner.NotReady();
		}
	}
}
