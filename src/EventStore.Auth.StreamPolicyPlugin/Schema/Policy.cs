// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json.Serialization;

namespace EventStore.Auth.StreamPolicyPlugin.Schema;

public class Policy : IJsonOnDeserialized {
	[JsonRequired]
	public Dictionary<string, AccessPolicy> StreamPolicies { get; init; } = default!;

	[JsonRequired]
	public StreamRule[] StreamRules { get; init; } = default!;

	[JsonRequired]
	public DefaultStreamRules DefaultStreamRules { get; init; } = default!;

	public void OnDeserialized() {
		if (StreamPolicies is null)
			throw new ArgumentNullException($"{nameof(StreamPolicies)} cannot be null");
		if (StreamRules is null)
			throw new ArgumentNullException($"{nameof(StreamRules)} cannot be null");
		if (DefaultStreamRules is null)
			throw new ArgumentNullException($"{nameof(DefaultStreamRules)} cannot be null");
	}
}
