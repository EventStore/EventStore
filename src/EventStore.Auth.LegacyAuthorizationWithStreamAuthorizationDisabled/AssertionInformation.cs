// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled;

public struct AssertionInformation {
	public Grant Grant { get; }
	private readonly string _assertion;

	public AssertionInformation(string type, string assertion, Grant grant) {
		Grant = grant;
		_assertion = $"{type}:{assertion}:{grant}";
	}

	public override string ToString() {
		return _assertion;
	}
}
