// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Claims;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Routing;

namespace EventStore.Auth.UserCertificates.Tests;

// pretends any user that the valid certificate names is in the database
internal class FakeAuthenticationProvider(string scheme) : AuthenticationProviderBase(name: "fake") {
	public override void Authenticate(AuthenticationRequest authenticationRequest) {
		if (!authenticationRequest.HasValidClientCertificate) {
			authenticationRequest.Unauthorized();
			return;
		}

		authenticationRequest.Authenticated(new ClaimsPrincipal(new ClaimsIdentity(
			new[] {
				new Claim(ClaimTypes.Name, authenticationRequest.Name),
			},
			"fake")));
	}

	public override IReadOnlyList<string> GetSupportedAuthenticationSchemes() => [scheme];
}
