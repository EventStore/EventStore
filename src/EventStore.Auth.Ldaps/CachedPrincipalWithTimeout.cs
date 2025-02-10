// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Security.Claims;

namespace EventStore.Auth.Ldaps;

internal class CachedPrincipalWithTimeout {
	public readonly string Password;
	public readonly ClaimsPrincipal Principal;
	public readonly DateTime ValidUntil;

	public CachedPrincipalWithTimeout(string password, ClaimsPrincipal principal, DateTime validUntil) {
		Password = password;
		Principal = principal;
		ValidUntil = validUntil;
	}
}
