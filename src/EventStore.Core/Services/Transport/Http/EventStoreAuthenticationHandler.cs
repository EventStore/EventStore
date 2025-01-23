// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EventStore.Core.Services.Transport.Http;

// This runs after Services.Transport.Http.AuthenticationMiddleware runs and makes use of the
// ClaimsPrinciple that it has left in the context.
// todo: figure out why AuthenticationMiddleware is as complicated as it is and whether it
// should be replaced with an AuthenticationHandler (or several)
public class EventStoreAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, UrlEncoder encoder)
	// we could pass the logger through instead of NullLoggerFactory, but we have debug logging
	// on by default, and it will log successes unless we change the level in the config:
	// "EventStore.Core.Services.Transport.Http.EventStoreAuthenticationHandler": "Information"
	// instead we should rationalize the log levels, turn off debug logging, and then pass this through.
	// ILoggerFactory logger,
	: AuthenticationHandler<AuthenticationSchemeOptions>(options, NullLoggerFactory.Instance, encoder) {
	protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
		if (!Context.User.Identity.IsAuthenticated)
			return Task.FromResult(AuthenticateResult.Fail("Not authenticated"));

		var ticket = new AuthenticationTicket(
			principal: Context.User,
			authenticationScheme: "es auth");

		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}
