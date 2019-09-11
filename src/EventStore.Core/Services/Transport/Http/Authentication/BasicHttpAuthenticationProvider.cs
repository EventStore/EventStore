using System.Net;
using System.Net.Http.Headers;
using System.Security.Principal;
using System.Text;
using EventStore.Core.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class BasicHttpAuthenticationProvider : HttpAuthenticationProvider {
		private readonly IAuthenticationProvider _internalAuthenticationProvider;

		public BasicHttpAuthenticationProvider(IAuthenticationProvider internalAuthenticationProvider) {
			_internalAuthenticationProvider = internalAuthenticationProvider;
		}

		public override bool Authenticate(IncomingHttpRequestMessage message) {
			//NOTE: this method can be invoked on multiple threads - needs to be thread safe
			var entity = message.Entity;

			if (!AuthenticationHeaderValue.TryParse(
					entity.Request.GetHeaderValues("authorization"),
					out var authenticationHeader)
				|| authenticationHeader.Scheme != "Basic"
				|| !TryDecodeCredential(authenticationHeader.Parameter, out var username, out var password)) {
				return false;
			}

			var authenticationRequest = new HttpBasicAuthenticationRequest(this, message, username, password);
			_internalAuthenticationProvider.Authenticate(authenticationRequest);
			return true;
		}

		private static bool TryDecodeCredential(string value, out string username, out string password) {
			username = password = default;
			var parts = Encoding.ASCII.GetString(System.Convert.FromBase64String(value)).Split(':'); // TODO: JOB Use Convert.TryFromBase64String when in dotnet core 3.0
			if (parts.Length != 2) {
				return false;
			}

			username = parts[0];
			password = parts[1];

			return true;
		}


		private class HttpBasicAuthenticationRequest : AuthenticationRequest {
			private readonly BasicHttpAuthenticationProvider _basicHttpAuthenticationProvider;
			private readonly IncomingHttpRequestMessage _message;

			public HttpBasicAuthenticationRequest(
				BasicHttpAuthenticationProvider basicHttpAuthenticationProvider, IncomingHttpRequestMessage message,
				string name, string suppliedPassword)
				: base(name, suppliedPassword) {
				_basicHttpAuthenticationProvider = basicHttpAuthenticationProvider;
				_message = message;
			}

			public override void Unauthorized() {
				ReplyUnauthorized(_message.Entity);
			}

			public override void Authenticated(IPrincipal principal) {
				_basicHttpAuthenticationProvider.Authenticated(_message, principal);
			}

			public override void Error() {
				ReplyInternalServerError(_message.Entity);
			}

			public override void NotReady() {
				ReplyNotYetAvailable(_message.Entity);
			}
		}
	}
}
