// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Kurrent.Connectors.Kafka;

namespace EventStore.Connectors.Infrastructure.Connect.Components.Connectors.KafkaSource;

[PublicAPI]
public static class KafkaWireup {
	public static ConsumerBuilder<string, byte[]> GetConsumer(KafkaSourceOptions options) {
		var consumerConfig = new ConsumerConfig(options.Consumer) {
			ClientId = options.Consumer.ClientId ?? typeof(Kurrent.Connectors.Kafka.KafkaSource).FullName,
			GroupId = options.Consumer.GroupId ?? options.Topic,
			TopicBlacklist = options.Consumer.TopicBlacklist ?? "^\\$.*"
		};
		return new ConsumerBuilder<string, byte[]>(consumerConfig);
	}

	public static ISchemaRegistryClient? GetSchemaRegistry(SchemaRegistryConfig options) =>
		!string.IsNullOrWhiteSpace(options.Url)
			? KafkaSchemaRegistryConfig.CreateSchemaRegistryClient(options)
			: null;
}
