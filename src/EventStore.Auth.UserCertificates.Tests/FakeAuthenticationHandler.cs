// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Encodings.Web;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventStore.Auth.UserCertificates.Tests;

// authenticates according to the httpAuthenticationProviders
// similar to EventStore.Core.Services.Transport.Http.AuthenticationMiddleware
public class FakeAuthenticationHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	IReadOnlyList<IHttpAuthenticationProvider> httpAuthenticationProviders,
	ILoggerFactory logger,
	UrlEncoder encoder)
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {

	protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
		if (!TrySelectProvider(Context, out var authenticationRequest)) {
			return AuthenticateResult.Fail("No provider");
		}

		var (status, principal) = await authenticationRequest!.AuthenticateAsync();

		if (status != HttpAuthenticationRequestStatus.Authenticated) {
			return AuthenticateResult.Fail("Not authenticated");
		}

		var ticket = new AuthenticationTicket(
			principal: principal!,
			authenticationScheme: "fake");

		return AuthenticateResult.Success(ticket);
	}

	private bool TrySelectProvider(HttpContext context, out HttpAuthenticationRequest? authenticationRequest) {
		foreach (var provider in httpAuthenticationProviders) {
			if (provider.Authenticate(context, out authenticationRequest)) {
				return true;
			}
		}

		authenticationRequest = default;
		return false;
	}
}
