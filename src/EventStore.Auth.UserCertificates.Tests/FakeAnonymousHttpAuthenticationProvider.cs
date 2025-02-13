// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Claims;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Http;

namespace EventStore.Auth.UserCertificates.Tests;

// grants anonymous claims to anyone
internal class FakeAnonymousHttpAuthenticationProvider : IHttpAuthenticationProvider {
	public static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity(new Claim[] { new(ClaimTypes.Anonymous, ""), }));
	public string Name => "anonymous";

	public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
		request = new HttpAuthenticationRequest(context, "", "");
		request.Authenticated(Anonymous);
		return true;
	}
}
