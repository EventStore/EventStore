using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Transport.Http;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http {
	public class AuthenticationMiddleware : IMiddleware {
		private readonly IReadOnlyList<IHttpAuthenticationProvider> _authenticationProviders;

		public AuthenticationMiddleware(IReadOnlyList<IHttpAuthenticationProvider> authenticationProviders) {
			_authenticationProviders = authenticationProviders;
		}

		public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
			for (int i = 0; i < _authenticationProviders.Count; i++) {
				if (_authenticationProviders[i].Authenticate(context, out var authenticationRequest)) {
					if (await authenticationRequest.AuthenticateAsync().ConfigureAwait(false)) {
						await next(context).ConfigureAwait(false);
						return;
					}

					break;
				}
			}

			context.Response.StatusCode = HttpStatusCode.Unauthorized;
		}
	}
}
