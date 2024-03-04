﻿using EventStore.Core.Services.UserManagement;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication {
	public class UnixSocketAuthenticationProvider : IHttpAuthenticationProvider {
		public string Name => "unix-socket";

		public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
			if (context.IsUnixSocketConnection()) {
				request = new HttpAuthenticationRequest(context, "system", "");
				request.Authenticated(SystemAccounts.System);
				return true;
			}

			request = null;
			return false;
		}
	}
}
