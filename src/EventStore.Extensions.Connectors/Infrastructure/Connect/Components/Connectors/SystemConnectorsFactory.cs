// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace

using EventStore.Connect.Processors;
using EventStore.Connect.Producers.Configuration;
using EventStore.Connect.Readers.Configuration;
using EventStore.Connectors;
using EventStore.Connectors.Connect.Components.Connectors;
using EventStore.Connectors.System;
using Kurrent.Surge.Connectors.Sinks;
using EventStore.Core.Bus;
using Kurrent.Surge;
using Kurrent.Surge.Connectors;
using Kurrent.Surge.Connectors.Sources;
using Kurrent.Surge.Consumers;
using Kurrent.Surge.Consumers.Configuration;
using Kurrent.Surge.Persistence.State;
using Kurrent.Surge.Processors;
using Kurrent.Surge.Processors.Configuration;
using Kurrent.Surge.Schema;
using Kurrent.Surge.SourceConnector;
using Kurrent.Surge.Transformers;
using Kurrent.Toolkit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AutoLockOptions = Kurrent.Surge.Processors.Configuration.AutoLockOptions;

namespace EventStore.Connect.Connectors;

public record SystemConnectorsFactoryOptions {
    public StreamTemplate                        CheckpointsStreamTemplate { get; init; } = ConnectorsFeatureConventions.Streams.CheckpointsStreamTemplate;
    public StreamTemplate                        LifecycleStreamTemplate   { get; init; } = ConnectorsFeatureConventions.Streams.LifecycleStreamTemplate;
    public AutoLockOptions                       AutoLock                  { get; init; } = new();
    public Func<IConfiguration, IConfiguration>? ProcessConfiguration      { get; init; }
}

public class SystemConnectorsFactory(SystemConnectorsFactoryOptions options, IServiceProvider services) : ISystemConnectorFactory {
    SystemConnectorsFactoryOptions Options  { get; } = options;
    IServiceProvider               Services { get; } = services;

    public IConnector CreateConnector(ConnectorId connectorId, IConfiguration configuration) {
        var connectorOptions = configuration.GetRequiredOptions<ConnectorOptions>();

        configuration = Options.ProcessConfiguration?.Invoke(configuration) ?? configuration;

        var connector = CreateConnectorInstance(connectorOptions.InstanceTypeName);

        return connector switch {
			ISink => CreateSinkConnector(),
			ISource source => CreateSourceConnector(source),
			_ => throw new ArgumentException($"Connector {connectorOptions.InstanceTypeName} is not a valid connector type")
		};

        SinkConnector CreateSinkConnector() {
	        var sinkOptions = configuration.GetRequiredOptions<SinkOptions>();

	        if (sinkOptions.Transformer.Enabled)
		        connector = new RecordTransformerSink(connector, new JintRecordTransformer(sinkOptions.Transformer.DecodeFunction()));

	        var sinkProxy = new SinkProxy(connectorId, connector, configuration, Services);

	        var processor = ConfigureProcessor(connectorId, sinkOptions, sinkProxy);

	        return new SinkConnector(processor, sinkProxy);
        }

        SourceConnector CreateSourceConnector(ISource source) {
	        var loggerFactory = Services.GetRequiredService<ILoggerFactory>();
	        var schemaRegistry = Services.GetRequiredService<SchemaRegistry>();
	        var readerBuilder = Services.GetRequiredService<Func<SystemReaderBuilder>>()();
	        var producerBuilder = Services.GetRequiredService<Func<SystemProducerBuilder>>()();
	        var getNodeSystemInfo = Services.GetRequiredService<GetNodeSystemInfo>();

	        var publishStateChangesOptions = new PublishStateChangesOptions {
		        Enabled        = true,
		        StreamTemplate = Options.LifecycleStreamTemplate
	        };

	        var nodeId = getNodeSystemInfo().AsTask().GetAwaiter().GetResult().InstanceId.ToString();

	        var autoLockOptions = Options.AutoLock with { OwnerId = nodeId };

	        return new SourceConnector(
		        new SourceConnectorOptions {
			        ConnectorId = connectorId,
			        SchemaRegistry = schemaRegistry,
			        Configuration = configuration,
			        AutoLock = autoLockOptions,
			        PublishStateChanges = publishStateChangesOptions,
			        ServiceProvider = Services
		        },
		        source,
		        readerBuilder,
		        producerBuilder,
		        loggerFactory
	        );
        }

        static dynamic CreateConnectorInstance(string connectorTypeName) {
            if (!ConnectorCatalogue.TryGetConnector(connectorTypeName, out var connector))
                throw new ArgumentException($"Failed to find connector {connectorTypeName}", nameof(connectorTypeName));

            return Activator.CreateInstance(connector.ConnectorType)!;
        }
    }

    IProcessor ConfigureProcessor(ConnectorId connectorId, SinkOptions sinkOptions, SinkProxy sinkProxy) {
        var publisher      = Services.GetRequiredService<IPublisher>();
        var loggerFactory  = Services.GetRequiredService<ILoggerFactory>();
        var schemaRegistry = Services.GetRequiredService<SchemaRegistry>();
        var stateStore     = Services.GetRequiredService<IStateStore>();

        // TODO SS: seriously, this is a bad idea, but creating a connector to be hosted in ESDB or PaaS is different from having full control of the Connect framework

        // It was either this, or having a separate configuration for the node or
        // even creating a factory provider that would create a new factory when the node becomes a leader,
        // and then it escalates because it would be the activator that would need to be recreated to "hide"
        // all this mess. Maybe these options would be passed on Create method and not on the factory...
        // I don't know, I'm just trying to make it work.
        // I'm not happy with this, but it's the best I could come up with in the time I had.

        var getNodeSystemInfo = Services.GetRequiredService<GetNodeSystemInfo>();

        var nodeId = getNodeSystemInfo().AsTask().GetAwaiter().GetResult().InstanceId.ToString();

        var filter = string.IsNullOrWhiteSpace(sinkOptions.Subscription.Filter.Expression)
            ? sinkOptions.Subscription.Filter.Scope is SinkConsumeFilterScope.Unspecified
                ? ConsumeFilter.ExcludeSystemEvents()
                : ConsumeFilter.None
            : ConsumeFilter.From(
                (ConsumeFilterScope)sinkOptions.Subscription.Filter.Scope,
                (ConsumeFilterType)sinkOptions.Subscription.Filter.FilterType,
                sinkOptions.Subscription.Filter.Expression
            );

        var publishStateChangesOptions = new PublishStateChangesOptions {
            Enabled        = true,
            StreamTemplate = Options.LifecycleStreamTemplate
        };

        var autoLockOptions = Options.AutoLock with { OwnerId = nodeId };

        var autoCommitOptions = new AutoCommitOptions {
            Enabled          = sinkOptions.AutoCommit.Enabled,
            Interval         = TimeSpan.FromMilliseconds(sinkOptions.AutoCommit.Interval),
            RecordsThreshold = sinkOptions.AutoCommit.RecordsThreshold,
            StreamTemplate   = Options.CheckpointsStreamTemplate
        };

        var loggingOptions = new Kurrent.Surge.Configuration.LoggingOptions {
            Enabled       = sinkOptions.Logging.Enabled,
            LogName       = sinkOptions.InstanceTypeName,
            LoggerFactory = loggerFactory
        };

        var builder = SystemProcessor.Builder
            .ProcessorId(connectorId)
            .Publisher(publisher)
            .StateStore(stateStore)
            .SchemaRegistry(schemaRegistry)
            .InitialPosition(sinkOptions.Subscription.InitialPosition)
            .PublishStateChanges(publishStateChangesOptions)
            .AutoLock(autoLockOptions)
            .Filter(filter)
            .Logging(loggingOptions)
            .AutoCommit(autoCommitOptions)
            .SkipDecoding()
            .WithHandler(sinkProxy);

        if (sinkOptions.Subscription.StartPosition is not null
         && sinkOptions.Subscription.StartPosition != RecordPosition.Unset
         && sinkOptions.Subscription.StartPosition != LogPosition.Unset) {
            builder = builder.StartPosition(sinkOptions.Subscription.StartPosition);
        }

        return builder.Create();
    }

    sealed class SinkConnector(IProcessor processor, SinkProxy sinkProxy) : IConnector {
        public ConnectorId    ConnectorId { get; } = ConnectorId.From(processor.ProcessorId);
        public ConnectorState State       { get; } = (ConnectorState)processor.State;

        public Task Stopped => processor.Stopped;

        public async Task Connect(CancellationToken stoppingToken) {
            await sinkProxy.Initialize(stoppingToken);
            await processor.Activate(stoppingToken);
        }

        public ValueTask DisposeAsync() =>
            processor.DisposeAsync();
    }
}
