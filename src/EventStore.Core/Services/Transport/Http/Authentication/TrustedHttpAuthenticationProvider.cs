using System;
using System.Security.Principal;
using EventStore.Core.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class TrustedHttpAuthenticationProvider : HttpAuthenticationProvider {
		public override bool Authenticate(IncomingHttpRequestMessage message) {
			var header = message.Entity.Request.Headers[SystemHeaders.TrustedAuth];
			if (!string.IsNullOrEmpty(header)) {
				var principal = CreatePrincipal(header);
				if (principal != null)
					Authenticated(message, principal);
				else
					ReplyUnauthorized(message.Entity);
				return true;
			}

			return false;
		}

		private IPrincipal CreatePrincipal(string header) {
			var loginAndGroups = header.Split(';');
			if (loginAndGroups.Length == 0 || loginAndGroups.Length > 2)
				return null;
			var login = loginAndGroups[0];
			if (loginAndGroups.Length == 2) {
				var groups = loginAndGroups[1];
				var groupsSplit = groups.Split(',');
				var roles = new string[groupsSplit.Length + 1];
				Array.Copy(groupsSplit, roles, groupsSplit.Length);
				roles[roles.Length - 1] = login;
				for (var i = 0; i < roles.Length; i++)
					roles[i] = roles[i].Trim();
				return new OpenGenericPrincipal(new GenericIdentity(login), roles);
			} else {
				return new OpenGenericPrincipal(new GenericIdentity(login), new[] {login});
			}
		}
	}
}
