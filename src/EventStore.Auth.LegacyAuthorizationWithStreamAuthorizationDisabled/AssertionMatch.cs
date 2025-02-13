// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled;

public readonly struct AssertionMatch {
	public readonly PolicyInformation Policy;
	public readonly AssertionInformation Assertion;
	public readonly IReadOnlyList<Claim> Matches;

	public AssertionMatch(PolicyInformation policy, AssertionInformation assertion, params Claim[] matches) : this(
		policy, assertion, (IReadOnlyList<Claim>)matches) {
	}

	public AssertionMatch(PolicyInformation policy, AssertionInformation assertion, IReadOnlyList<Claim> matches) {
		Policy = policy;
		Assertion = assertion;
		Matches = matches;
	}

	public override string ToString() {
		return $"{Policy} : {Assertion}, ${string.Join(Environment.NewLine + "\t", Matches)}";
	}
}
