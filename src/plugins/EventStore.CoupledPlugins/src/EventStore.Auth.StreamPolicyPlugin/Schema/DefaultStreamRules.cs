// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json.Serialization;

namespace EventStore.Auth.StreamPolicyPlugin.Schema;

public class DefaultStreamRules : IJsonOnDeserialized {
	[JsonRequired]
	public string UserStreams { get; init; } = default!;

	[JsonRequired]
	public string SystemStreams { get; init; } = default!;

	public void OnDeserialized() {
		if (string.IsNullOrEmpty(UserStreams))
			throw new ArgumentNullException($"{nameof(UserStreams)} cannot be null or empty");
		if (string.IsNullOrEmpty(SystemStreams))
			throw new ArgumentNullException($"{nameof(SystemStreams)} cannot be null or empty");
	}
}
