// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using Confluent.Kafka;
using Confluent.SchemaRegistry;
using EventStore.Streaming.Connectors.Sinks;

namespace Kurrent.Connectors.Kafka;

[PublicAPI]
public record KafkaSourceOptions : SourceOptions {
	public string Topic { get; init; } = string.Empty;

	public int? Partition { get; init; }

	public int? Offset { get; init; }

	public ConsumerConfig Consumer { get; init; } = new();

	public SchemaRegistryConfig SchemaRegistry { get; init; } = new();
}
