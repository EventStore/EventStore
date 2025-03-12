// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json.Serialization;

namespace EventStore.Auth.StreamPolicyPlugin.Schema;

public class StreamRule : IJsonOnDeserialized {
	[JsonRequired]
	public string StartsWith { get; init; } = default!;

	[JsonRequired]
	public string Policy { get; init; } = default!;
	public void OnDeserialized() {
		if (string.IsNullOrEmpty(StartsWith))
			throw new ArgumentNullException($"{nameof(StartsWith)} cannot be null or empty");
		if (string.IsNullOrEmpty(Policy))
			throw new ArgumentNullException($"{nameof(Policy)} cannot be null or empty");
	}
}
