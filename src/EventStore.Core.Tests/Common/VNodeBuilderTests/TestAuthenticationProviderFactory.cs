using System.Security.Claims;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Services;
using EventStore.Core.Services.Transport.Http;

namespace EventStore.Core.Tests.Common.VNodeBuilderTests {
	public class TestAuthenticationProviderFactory : IAuthenticationProviderFactory {
		public IAuthenticationProvider BuildAuthenticationProvider(bool logFailedAuthenticationAttempts) {
			return new TestAuthenticationProvider();
		}

		public void RegisterHttpControllers(IHttpService externalHttpService, HttpSendService httpSendService,
			IPublisher mainQueue, IPublisher networkSendQueue) {
		}
	}

	public class TestAuthenticationProvider : IAuthenticationProvider {
		public void Authenticate(AuthenticationRequest authenticationRequest) {
			authenticationRequest.Authenticated(new ClaimsPrincipal(new ClaimsIdentity(new []{new Claim(ClaimTypes.Name, authenticationRequest.Name), })));
		}
	}
}
