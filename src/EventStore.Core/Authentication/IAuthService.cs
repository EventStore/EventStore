// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EventStore.Core.Authentication;

/// <summary>
/// Interface for authentication service that is used by Blazor UI
/// </summary>
public interface IAuthService {
	ClaimsPrincipal CurrentUser { get; }
	bool IsLoggedIn { get; }
	Task Login(ClaimsPrincipal user, bool storeToken = true);
	Task Logout();
	Task<bool> GetStateFromTokenAsync();
	event Action<ClaimsPrincipal> UserChanged;
}
