using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Plugins.Authentication;
using EventStore.Transport.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace EventStore.Core.Services.Transport.Http {
	public class AuthenticationMiddleware : IMiddleware {
		private readonly IAuthenticationProvider _authenticationProvider;
		private readonly IReadOnlyList<IHttpAuthenticationProvider> _httpAuthenticationProviders;

		public AuthenticationMiddleware(IReadOnlyList<IHttpAuthenticationProvider> httpAuthenticationProviders, IAuthenticationProvider authenticationProvider) {
			_httpAuthenticationProviders = httpAuthenticationProviders;
			_authenticationProvider = authenticationProvider;
		}

		public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
			try {
				for (int i = 0; i < _httpAuthenticationProviders.Count; i++) {
					if (_httpAuthenticationProviders[i].Authenticate(context, out var authenticationRequest)) {
						if (await authenticationRequest.AuthenticateAsync().ConfigureAwait(false)) {
							await next(context).ConfigureAwait(false);
							return;
						}

						break;
					}
				}

				context.Response.StatusCode = HttpStatusCode.Unauthorized;
				var authSchemes = _authenticationProvider.GetSupportedAuthenticationSchemes();
				if (authSchemes != null && authSchemes.Any()) {
					//add "X-" in front to prevent any default browser behaviour e.g Basic Auth popups
					context.Response.Headers.Add("WWW-Authenticate", $"X-{authSchemes.First()} realm=\"ESDB\"");
					var properties = _authenticationProvider.GetPublicProperties();
					if (properties != null && properties.Any()) {
						await context.Response.WriteAsync(JsonConvert.SerializeObject(properties))
							.ConfigureAwait(false);
					}
				}
			} catch (Exception ex) when (ex is OperationCanceledException or TaskCanceledException){
				//ignore request aborted
			}
		}
	}
}
