using System;
using EventStore.Core.Authentication.InternalAuthentication;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class PassthroughHttpAuthenticationProvider : IHttpAuthenticationProvider {
		private readonly PassthroughAuthenticationProvider _passthroughAuthenticationProvider;

		public PassthroughHttpAuthenticationProvider(IAuthenticationProvider internalAuthenticationProvider) {
			if (!(internalAuthenticationProvider is PassthroughAuthenticationProvider passthroughAuthenticationProvider))
				throw new ArgumentException("PassthroughHttpAuthenticationProvider can be initialized only with a PassthroughAuthenticationProvider");
			_passthroughAuthenticationProvider = passthroughAuthenticationProvider;
		}

		public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
			request = new HttpAuthenticationRequest(context, null, null);
			_passthroughAuthenticationProvider.Authenticate(request);
			return true;
		}
	}
}
