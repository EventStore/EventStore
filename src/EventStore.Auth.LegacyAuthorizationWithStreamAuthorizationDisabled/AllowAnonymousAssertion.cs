// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Claims;
using System.Threading.Tasks;
using EventStore.Plugins.Authorization;

namespace EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled;

internal class AllowAnonymousAssertion : IAssertion {
	public Grant Grant { get; } = Grant.Allow;

	public AssertionInformation Information { get; } =
		new AssertionInformation("authenticated", "not anonymous", Grant.Unknown);

	public ValueTask<bool> Evaluate(ClaimsPrincipal cp, Operation operation, PolicyInformation policy,
		EvaluationContext context) {
		context.Add(new AssertionMatch(policy, new AssertionInformation("match", "allow anonymous", Grant.Allow)));
		return new ValueTask<bool>(true);
	}
}
