// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled;

public class PolicyInformation {
	public readonly DateTimeOffset Expires;
	public readonly string Name;

	public PolicyInformation(string name, long version, DateTimeOffset expires) {
		Version = version;
		Name = name;
		Expires = expires;
	}

	public long Version { get; }

	public override string ToString() {
		return $"Policy : {Name} {Version} {Expires}";
	}
}
