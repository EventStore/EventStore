// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Logging;

namespace KurrentDB.Services;

public class AuthStateProvider : RevalidatingServerAuthenticationStateProvider {
	AuthenticationState _authenticationState;

	public AuthStateProvider(IAuthService service, ILoggerFactory loggerFactory) : base(loggerFactory) {
		_authenticationState = new(service.CurrentUser);
		service.UserChanged += newUser => {
			_authenticationState = new(newUser);
			NotifyAuthenticationStateChanged(Task.FromResult(_authenticationState));
		};
	}

	public override Task<AuthenticationState> GetAuthenticationStateAsync() {
		return Task.FromResult(_authenticationState);
	}

	protected override Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState, CancellationToken cancellationToken) {
		return Task.FromResult(authenticationState.User.Identity?.IsAuthenticated == true);
	}

	protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);
}
