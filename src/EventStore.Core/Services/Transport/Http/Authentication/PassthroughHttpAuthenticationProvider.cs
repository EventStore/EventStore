// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using EventStore.Core.Authentication.DelegatedAuthentication;
using EventStore.Core.Authentication.PassthroughAuthentication;
using EventStore.Plugins.Authentication;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http.Authentication;

public class PassthroughHttpAuthenticationProvider : IHttpAuthenticationProvider {
	private readonly IAuthenticationProvider _passthroughAuthenticationProvider;

	public string Name => "insecure";

	public PassthroughHttpAuthenticationProvider(IAuthenticationProvider internalAuthenticationProvider) {
		_passthroughAuthenticationProvider = GetProvider(internalAuthenticationProvider);
	}

	private static PassthroughAuthenticationProvider GetProvider(IAuthenticationProvider provider) =>
		provider switch {
			PassthroughAuthenticationProvider p => p,
			DelegatedAuthenticationProvider d => GetProvider(d.Inner),
			_ => throw new ArgumentException(
				"PassthroughHttpAuthenticationProvider can be initialized only with a PassthroughAuthenticationProvider.")
		};

	public bool Authenticate(HttpContext context, out HttpAuthenticationRequest request) {
		request = new HttpAuthenticationRequest(context, null, null);
		_passthroughAuthenticationProvider.Authenticate(request);
		return true;
	}
}
