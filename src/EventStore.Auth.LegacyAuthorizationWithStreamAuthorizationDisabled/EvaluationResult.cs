// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Plugins.Authorization;

namespace EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled;

public readonly struct EvaluationResult {
	public readonly Grant Grant;
	public readonly Operation Operation;
	public readonly IReadOnlyList<AssertionMatch> Matches;

	public EvaluationResult(Operation operation, Grant grant, params AssertionMatch[] matches) : this(operation,
		grant, (IReadOnlyList<AssertionMatch>)matches) {
	}

	public EvaluationResult(Operation operation, Grant grant, IReadOnlyList<AssertionMatch> matches) {
		Grant = grant;
		Matches = matches;
		Operation = operation;
	}

	public override string ToString() {
		return $"{Operation} {Grant} : {string.Join(Environment.NewLine, Matches)}";
	}
}
