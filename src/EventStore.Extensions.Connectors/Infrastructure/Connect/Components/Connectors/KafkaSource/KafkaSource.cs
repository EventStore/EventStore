// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

// ReSharper disable InconsistentNaming
// ReSharper disable ArrangeTypeModifiers
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InvertIf

using System.Threading.Channels;
using Confluent.Kafka;
using EventStore.Connectors.Infrastructure.Connect.Components.Connectors.KafkaSource;
using Kurrent.Surge;
using Kurrent.Surge.Connectors.Sinks;
using Kurrent.Surge.Connectors.Sources;
using Kurrent.Surge.Schema;
using Kurrent.Toolkit;
using Microsoft.Extensions.Logging;
using Headers = Kurrent.Surge.Headers;
using ISchemaRegistryClient = Confluent.SchemaRegistry.ISchemaRegistryClient;
using TopicPartitionOffsets = System.Collections.Concurrent.ConcurrentDictionary<
	Kurrent.Surge.RecordId,
	Confluent.Kafka.TopicPartitionOffset
>;

namespace Kurrent.Connectors.Kafka;

class KafkaSource : ISource {
    IConsumer<string, byte[]> Consumer = null!;
    ISchemaRegistryClient?    SchemaRegistryClient;
    TopicPartitionOffsets     Positions    { get; }      = [];
    Task                      ConsumerTask { get; set; } = null!;

    readonly Channel<SurgeRecord> InboundChannel = Channel.CreateBounded<SurgeRecord>(
        new BoundedChannelOptions(100) {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        }
    );

    public ValueTask Open(SourceOpenContext ctx) {
        var options = ctx.Configuration.GetRequiredOptions<KafkaSourceOptions>();

        SchemaRegistryClient = KafkaWireup.GetSchemaRegistry(options.SchemaRegistry);

        Consumer = KafkaWireup.GetConsumer(options)
            .SetPartitionsLostHandler((consumer, positions) => consumer.Commit(positions))
            .SetPartitionsRevokedHandler((consumer, positions) => consumer.Commit(positions))
            .Build();

        Consumer.Subscribe(options.Topic, options.Partition, options.Offset);

        ConsumerTask = Task.Run(
            async () => {
                while (!ctx.CancellationToken.IsCancellationRequested) {
                    try {
                        var result = Consumer.Consume(ctx.CancellationToken);

                        var record = new SurgeRecord {
                            Id        = RecordId.From(Guid.NewGuid()),
                            Timestamp = result.Message.Timestamp.UtcDateTime,
                            Value     = result.Message.Value,
                            ValueType = result.Message.Value.GetType(),
                            Position = new RecordPosition {
                                StreamId    = options.Topic,
                                PartitionId = PartitionId.From(result.Partition),
                            }
                        };

                        record = await record.Enrich(SchemaRegistryClient, result.Message);

                        Positions[record.Id] = result.TopicPartitionOffset;

                        await InboundChannel.Writer.WriteAsync(record, ctx.CancellationToken);
                    }
                    catch (OperationCanceledException) when (ctx.CancellationToken.IsCancellationRequested) {
                        break;
                    }
                    catch (KafkaException kex) when (!kex.IsTransient()) {
                        ctx.Logger.LogError("Failed to consume message from topic {Topic}: {Exception}", options.Topic, kex);
                        await Flush();
                    }
                }

                InboundChannel.Writer.Complete();
            },
            ctx.CancellationToken
        );

        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<SurgeRecord> Read(CancellationToken stoppingToken) =>
        InboundChannel.Reader.ReadAllAsync(stoppingToken);

    public ValueTask Track(SurgeRecord record) {
        if (Positions.TryGetValue(record.Id, out var position)) {
            Consumer.StoreOffset(position);
            Positions.TryRemove(record.Id, out _);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask Commit(SurgeRecord record) {
        if (Positions.TryGetValue(record.Id, out var position)) {
            Consumer.Commit([position]);
            Positions.TryRemove(record.Id, out _);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask CommitAll() {
        Consumer.Commit();
        Positions.Clear();
        return ValueTask.CompletedTask;
    }

    public async ValueTask Close(SourceCloseContext ctx) =>
        await Flush();

    async ValueTask Flush() {
        try {
            Consumer.Unsubscribe();
            Consumer.Close();
            Consumer.Dispose();
        }
        catch (KafkaException kex) when (!kex.IsTransient()) {
            // ignore
        }
        finally {
            SchemaRegistryClient?.Dispose();

            InboundChannel.Writer.TryComplete();

            await ConsumerTask;
        }
    }
}

internal static class SurgeRecordExtensions {
    public static async Task<SurgeRecord> Enrich(this SurgeRecord record, ISchemaRegistryClient? schemaRegistryClient, Message<string, byte[]> message) {
        var data       = message.Value;
        var schemaInfo = SchemaInfo.None;
        var headers    = new Headers();
        var recordData = new ReadOnlyMemory<byte>(data);

        record.InjectDefaultSinkHeaders((key, value) => headers.Add(key, value));

        if (schemaRegistryClient is null) {
			return record with {
                Data = recordData,
                SchemaInfo = schemaInfo,
                Headers = headers
            };
		}

		var schemaId = KurrentSchemaRegistry.ParseSchemaId(data);
        var schema   = await schemaRegistryClient.GetSchemaAsync(schemaId);

        headers.Add(HeaderKeys.PartitionKey, message.Key.Normalize());
        headers.Add(HeaderKeys.SchemaSubject, $"{record.Id}-value"); // depends on how SubjectNameStrategy is configured in producer
        headers.Add(HeaderKeys.SchemaType, schema.SchemaType.ToString().ToLower());

        schemaInfo = SchemaInfo.FromHeaders(headers);
        recordData = KurrentSchemaRegistry.ExtractMessagePayload(data);

        return record with {
            Data = recordData,
            SchemaInfo = schemaInfo,
            Headers = headers
        };
    }
}

public static class KafkaConsumerExtensions {
    public static void Subscribe<TKey, TValue>(this IConsumer<TKey, TValue> consumer, string topic, int? partition, long? offset) {
		if (partition is null) {
			consumer.Subscribe(topic);
		} else {
			consumer.Assign(new TopicPartition(topic, partition.Value));
			consumer.Consume();

			if (offset is not null)
				consumer.Seek(new TopicPartitionOffset(topic, partition.Value, offset.Value));
		}
	}
}
