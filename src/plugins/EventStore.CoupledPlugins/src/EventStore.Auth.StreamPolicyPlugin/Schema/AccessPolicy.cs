// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json.Serialization;

namespace EventStore.Auth.StreamPolicyPlugin.Schema;

public class AccessPolicy : IJsonOnDeserialized {
	[JsonRequired]
	[JsonPropertyName("$r")]
	public string[] Readers { get; init; } = default!;

	[JsonRequired]
	[JsonPropertyName("$w")]
	public string[] Writers { get; init; } = default!;

	[JsonRequired]
	[JsonPropertyName("$d")]
	public string[] Deleters { get; init; } = default!;

	[JsonRequired]
	[JsonPropertyName("$mr")]
	public string[] MetadataReaders { get; init; } = default!;

	[JsonRequired]
	[JsonPropertyName("$mw")]
	public string[] MetadataWriters { get; init; } = default!;

	public void OnDeserialized() {
		if (Readers is null)
			throw new ArgumentNullException($"{nameof(Readers)} cannot be null");
		if (Writers is null)
			throw new ArgumentNullException($"{nameof(Writers)} cannot be null");
		if (Deleters is null)
			throw new ArgumentNullException($"{nameof(Deleters)} cannot be null");
		if (MetadataReaders is null)
			throw new ArgumentNullException($"{nameof(MetadataReaders)} cannot be null");
		if (MetadataWriters is null)
			throw new ArgumentNullException($"{nameof(MetadataWriters)} cannot be null");
	}
}
