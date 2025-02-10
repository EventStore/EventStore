// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Claims;

namespace EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled;

internal static class WellKnownAssertions {
	public static readonly IAssertion System = new MultipleClaimMatchAssertion(Grant.Allow, MultipleMatchMode.All,
		new Claim(ClaimTypes.Name, "system"), new Claim(ClaimTypes.Authentication, "ES-Legacy"));

	public static readonly IAssertion Admin = new MultipleClaimMatchAssertion(Grant.Allow, MultipleMatchMode.Any,
		new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, SystemRoles.Admins));
}
