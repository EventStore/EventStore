// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Claims;
using System.Threading.Tasks;
using EventStore.Plugins.Authorization;

namespace EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled;

internal interface IAssertion {
	Grant Grant { get; }
	AssertionInformation Information { get; }

	ValueTask<bool> Evaluate(ClaimsPrincipal cp, Operation operation, PolicyInformation policy,
		EvaluationContext context);
}
