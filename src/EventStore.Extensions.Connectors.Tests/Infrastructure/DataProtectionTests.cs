// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using EventStore.Connect.Connectors;
using EventStore.Connectors.Management;
using EventStore.Connectors.Management.Contracts.Events;
using EventStore.Connectors.Management.Contracts.Queries;
using EventStore.Connectors.Management.Data;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Kurrent.Connectors.Kafka;
using Kurrent.Surge.Connectors.Sinks;
using Kurrent.Surge.DataProtection;
using Kurrent.Surge.Processors;
using Microsoft.Extensions.Configuration;

namespace EventStore.Extensions.Connectors.Tests;

public class DataProtectionTests(ITestOutputHelper output, ConnectorsAssemblyFixture fixture) : ConnectorsIntegrationTests(output, fixture) {
    [Fact]
    public Task data_protector_should_protect_and_unprotect_successfully() => Fixture.TestWithTimeout(async cts => {
        // Arrange
        var connectorDataProtector = ConnectorsMasterDataProtector.Instance;

        const string key              = "Authentication:Password";
        const string value            = "plaintext";
        const string instanceTypeName = nameof(SinkOptions.InstanceTypeName);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> {
                [instanceTypeName] = nameof(KafkaSink),
                [key]              = value
            }!).Build();

        // Act
        configuration[key] = await Fixture.DataProtector.Protect(configuration[key]!, cts.Token);

        var options = new SystemConnectorsFactoryOptions {
            ProcessConfiguration = config => connectorDataProtector.Unprotect(config, Fixture.DataProtector, cts.Token).AsTask().GetAwaiter().GetResult()
        };

        var updated = options.ProcessConfiguration?.Invoke(configuration);

        configuration = updated ?? configuration;

        // Assert
        configuration[key].Should().BeEquivalentTo(value);
    });

    [Fact]
    public Task data_protector_should_protect_and_store_command_settings_in_snapshot() => Fixture.TestWithTimeout(async cts => {
        // Arrange
        var connectorId = Fixture.NewConnectorId();
        var streamId    = Fixture.NewIdentifier();

        var createTimestamp        = Fixture.TimeProvider.GetUtcNow().ToTimestamp();
        var projection             = new ConnectorsStateProjection(Fixture.SnapshotProjectionsStore, streamId);
        var connectorDataProtector = ConnectorsMasterDataProtector.Instance;

        const string key   = "Authentication:Password";
        const string value = "secret";

        var cmdSettings = new Dictionary<string, string> {
            { "instanceTypeName", "kafka-sink" },
            { key, value }
        };

        var settings = ConnectorSettings
            .From(cmdSettings!)
            .Protect(connectorId, ProtectSettings)
            .AsDictionary();

        var cmd = new ConnectorCreated {
            ConnectorId = connectorId,
            Name        = "Kafka Sink",
            Settings    = { settings },
            Timestamp   = createTimestamp
        };

        // Act
        await projection.ProcessRecord(await RecordContextFor(cmd, cts.Token));

        // Assert
        var store = await Fixture.SnapshotProjectionsStore.LoadSnapshot<ConnectorsSnapshot>(streamId);

        store.Snapshot.Connectors.Should().ContainSingle();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(store.Snapshot.Connectors.First().Settings)
            .Build();

        var unprotected = connectorDataProtector.Unprotect(configuration, Fixture.DataProtector, cts.Token).AsTask().GetAwaiter().GetResult();

        unprotected[key].Should().BeEquivalentTo(value);

        return;

        ValueTask<IDictionary<string, string?>> ProtectSettings(string innerConnectorId, IDictionary<string, string?> innerSettings) =>
            connectorDataProtector.Protect(connectorId: innerConnectorId,
                settings: new Dictionary<string, string?>(innerSettings, StringComparer.OrdinalIgnoreCase),
                dataProtector: Fixture.DataProtector,
                ct: cts.Token);
    });

    async Task<RecordContext> RecordContextFor<T>(T cmd, CancellationToken cancellationToken) where T : IMessage {
        string connectorId = ((dynamic)cmd).ConnectorId;
        var    record      = await Fixture.CreateRecord(cmd);
        return Fixture.CreateRecordContext(connectorId, cancellationToken) with { Record = record };
    }
}
