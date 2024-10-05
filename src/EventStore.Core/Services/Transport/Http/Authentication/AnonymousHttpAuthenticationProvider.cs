// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Services.UserManagement;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication;

public class AnonymousHttpAuthenticationProvider : IHttpAuthenticationProvider {
	public string Name => "anonymous";

	public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
		request = new HttpAuthenticationRequest(context, null, null);
		request.Authenticated(SystemAccounts.Anonymous);
		return true;
	}
}
