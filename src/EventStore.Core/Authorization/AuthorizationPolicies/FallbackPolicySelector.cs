// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Security.Claims;
using EventStore.Core.Services;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization.AuthorizationPolicies;

/// <summary>
/// This policy selector is used if there are entries in the settings stream
/// but none of them are valid or can be started.
/// Only admins have stream access to prevent falling back to a more permissive policy.
/// </summary>
public class FallbackPolicySelector : IPolicySelector {
	public const string FallbackPolicyName = "system-fallback";
	private static readonly Claim[] Admins =
		{new Claim(ClaimTypes.Role, SystemRoles.Admins), new Claim(ClaimTypes.Name, SystemUsers.Admin)};
	public ReadOnlyPolicy Select() {
		var isAdmin = new MultipleClaimMatchAssertion(Grant.Allow, MultipleMatchMode.Any, Admins);
		var policy = new Policy(FallbackPolicyName, 1, DateTimeOffset.MinValue);
		policy.Add(Operations.Streams.Read, isAdmin);
		policy.Add(Operations.Streams.Write, isAdmin);
		policy.Add(Operations.Streams.Delete, isAdmin);
		policy.Add(Operations.Streams.MetadataRead, isAdmin);
		policy.Add(Operations.Streams.MetadataWrite, isAdmin);
		return policy.AsReadOnly();
	}
}
