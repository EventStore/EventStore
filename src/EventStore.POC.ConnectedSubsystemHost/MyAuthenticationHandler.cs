// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using EventStore.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventStore.POC.PluginHost;

//qq for preview purposes:
// uses the username and password provided on the call to determine if this user is an admin by
// issuing a $all read to the es server. longer term the es server ought probably to provide
// some identity capabilities, and this host should be able to get an identity from there or elsewhere
public class MyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions> {
	private readonly EventStoreClient _client;

	public MyAuthenticationHandler(
		EventStoreClient client,
		IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder)
		: base(options, logger, encoder) {

		_client = client;
	}

	protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
		if (!TryDecodeCredential(Request, out var username, out var password)) {
			return AuthenticateResult.Fail("Username or password not provided");
		}
		try {
			//qq consider retries
			await foreach (var x in _client.ReadAllAsync(
				Direction.Backwards,
				Position.End,
				maxCount: 1,
				deadline: TimeSpan.FromSeconds(10),
				userCredentials: new(username, password))) {

				;
			}
		}
		catch (Exception ex) {
			return AuthenticateResult.Fail(ex);
		}

		var ticket = new AuthenticationTicket(
			principal: new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
				new(ClaimTypes.Role, SystemRoles.Admins),
			})),
			authenticationScheme: "event store query");

		return AuthenticateResult.Success(ticket);
	}

	//qq we really have to write this ourselves? got it from BasicHttpAuthenticationProvider
	private static bool TryDecodeCredential(HttpRequest request, out string username, out string password) {
		username = password = "";
		return request.Headers.TryGetValue("authorization", out var values) && values.Count == 1 &&
			AuthenticationHeaderValue.TryParse(values[0], out var authenticationHeader) &&
			authenticationHeader.Scheme == "Basic" &&
			TryDecodeCredential(authenticationHeader.Parameter ?? "", out username, out password);
	}

	private static bool TryDecodeCredential(string value, out string username, out string password) {
		username = password = "";

		var parts = Encoding.UTF8.GetString(System.Convert.FromBase64String(value)).Split(':', 2);
		if (parts.Length < 2) {
			return false;
		}

		username = parts[0];
		password = parts[1];

		return true;
	}
}
