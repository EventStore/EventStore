using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using EventStore.Core.Services.Transport.Http.Messages;
using EventStore.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public abstract class HttpAuthenticationProvider {
		public abstract bool Authenticate(IncomingHttpRequestMessage message);

		protected void Authenticated(IncomingHttpRequestMessage message, IPrincipal user) {
			var authenticatedEntity = message.Entity.SetUser(user);
			message.NextStagePublisher.Publish(
				new AuthenticatedHttpRequestMessage(message.HttpService, authenticatedEntity));
		}

		public static void ReplyUnauthorized(HttpEntity entity) {
			var manager = entity.CreateManager();
			manager.ReplyStatus(HttpStatusCode.Unauthorized, "Unauthorized", exception => { });
		}

		public static void ReplyInternalServerError(HttpEntity entity) {
			var manager = entity.CreateManager();
			manager.ReplyStatus(HttpStatusCode.InternalServerError, "Internal Server Error", exception => { });
		}

		public static void ReplyNotYetAvailable(HttpEntity entity) {
			var manager = entity.CreateManager();
			manager.ReplyStatus(HttpStatusCode.ServiceUnavailable, "Not yet ready.", exception => { },
				new[] {new KeyValuePair<string, string>("Retry-After", "5")});
		}
	}
}
