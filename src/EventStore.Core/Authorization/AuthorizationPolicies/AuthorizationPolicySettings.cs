// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Authorization.AuthorizationPolicies;

public class AuthorizationPolicySettings {
	public string StreamAccessPolicyType { get; set; }

	public AuthorizationPolicySettings() {
		StreamAccessPolicyType = FallbackStreamAccessPolicySelector.FallbackPolicyName;
	}

	public AuthorizationPolicySettings(string streamAccessPolicyType) {
		StreamAccessPolicyType = streamAccessPolicyType;
	}
	public override string ToString() => $"{nameof(StreamAccessPolicyType)}: '{StreamAccessPolicyType}'";
}
