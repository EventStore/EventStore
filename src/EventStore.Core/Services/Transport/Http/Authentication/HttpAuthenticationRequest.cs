using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Authentication;
using EventStore.Transport.Http;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class HttpAuthenticationRequest : AuthenticationRequest {
		private readonly HttpContext _context;
		private readonly TaskCompletionSource<bool> _tcs;
		private readonly CancellationTokenRegistration _cancellationRegister;

		public HttpAuthenticationRequest(HttpContext context, string authToken) : this(context,
			new Dictionary<string, string> {
				["jwt"] = authToken
			}) {
		}

		public HttpAuthenticationRequest(HttpContext context, string name, string suppliedPassword) :
			this(context, new Dictionary<string, string> {
				["uid"] = name,
				["pwd"] = suppliedPassword
			}) {
		}

		private HttpAuthenticationRequest(HttpContext context, IReadOnlyDictionary<string, string> tokens) : base(
			context.TraceIdentifier, tokens) {
			_context = context;
			_tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			_cancellationRegister = _context.RequestAborted.Register(Cancel);
		}

		private void Cancel() {
			_tcs.TrySetCanceled(_context.RequestAborted);
			_cancellationRegister.Dispose();
		}

		public override void Unauthorized() {
			_context.Response.StatusCode = HttpStatusCode.Unauthorized;
			_tcs.TrySetResult(false);
		}

		public override void Authenticated(ClaimsPrincipal principal) {
			_context.User = principal;
			_tcs.TrySetResult(true);
		}

		public override void Error() {
			_context.Response.StatusCode = HttpStatusCode.InternalServerError;
			_tcs.TrySetResult(false);
		}

		public override void NotReady() {
			_context.Response.StatusCode = HttpStatusCode.ServiceUnavailable;
			_context.Response.Headers.Add("Retry-After", "5");
			_tcs.TrySetResult(false);
		}

		public Task<bool> AuthenticateAsync() => _tcs.Task;
	}
}
