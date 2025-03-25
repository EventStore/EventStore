using Confluent.SchemaRegistry;

namespace Kurrent.Connectors.Kafka;

internal static class KafkaSchemaRegistryConfig {
    public static ISchemaRegistryClient CreateSchemaRegistryClient(SchemaRegistryConfig config) =>
        new CachedSchemaRegistryClient(config);
}
