// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Authorization;

namespace EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled;

public interface IPolicyEvaluator {
	ValueTask<EvaluationResult> EvaluateAsync(ClaimsPrincipal cp, Operation operation,
		CancellationToken cancellationToken);
}
