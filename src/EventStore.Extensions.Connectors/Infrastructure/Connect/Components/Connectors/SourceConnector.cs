// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

// ReSharper disable ArrangeObjectCreationWhenTypeNotEvident

using Kurrent.Surge.Connectors;
using Kurrent.Surge.Connectors.Sources;
using Kurrent.Surge.Interceptors;
using Kurrent.Surge.Leases;
using Kurrent.Surge.Processors;
using Kurrent.Surge.Processors.Interceptors;
using Kurrent.Surge.Processors.Locks;
using Kurrent.Surge.Producers;
using Kurrent.Surge.Producers.Configuration;
using Kurrent.Surge.Readers;
using Kurrent.Surge.Readers.Configuration;
using Kurrent.Surge.SourceConnector;
using Kurrent.Toolkit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NodaTime.Extensions;
using static System.Threading.Tasks.TaskCreationOptions;

namespace Kurrent.Surge.Connectors.Sources;

[PublicAPI]
public partial class SourceConnector : BackgroundService, IConnector {
    public SourceConnector(
        SourceConnectorOptions options,
        ISource source,
        IReaderBuilder readerBuilder,
        IProducerBuilder producerBuilder,
        ILoggerFactory loggerFactory
    ) {
        Options       = options;
        Source        = source;
        LoggerFactory = loggerFactory;
        Logger        = LoggerFactory.CreateLogger(GetType().Name);
        ConnectorId   = options.ConnectorId;

        Producer = producerBuilder
            .DisableResiliencePipeline()
            .LoggerFactory(LoggerFactory)
            .Create();

        Reader = readerBuilder
            .LoggerFactory(loggerFactory)
            .SchemaRegistry(options.SchemaRegistry)
            .Create();

        ConnectorMetadata = new ConnectorMetadata {
            ConnectorId = Options.ConnectorId,
            ClientId    = $"{Options.ConnectorId}-{Guid.NewGuid().ToString()[30..]}"
        };

        LinkedList<InterceptorModule> interceptors = new();

        if (options.PublishStateChanges.Enabled) {
            interceptors.TryAddUniqueFirst(
                new ProcessorStateChangePublisher(
                    Producer,
                    options.PublishStateChanges.StreamTemplate.GetStream(Options.ConnectorId),
                    options.ConnectorId
                )
            );
        }

        Interceptors = new(interceptors, Logger);

        var leaseManager = new LeaseManager(
            Reader,
            Producer,
            streamTemplate: options.AutoLock.StreamTemplate,
            logger: LoggerFactory.CreateLogger<LeaseManager>()
        );

        ServiceLocker = new ServiceLocker(
            new ServiceLockerOptions {
                ResourceId    = Guid.NewGuid().ToString(),
                OwnerId       = Options.AutoLock.OwnerId,
                LeaseDuration = Options.AutoLock.LeaseDuration.ToDuration(),
                Retry = new() {
                    Timeout = Options.AutoLock.AcquisitionTimeout.ToDuration(),
                    Delay   = Options.AutoLock.AcquisitionDelay.ToDuration()
                }
            },
            leaseManager
        );

        Finisher = new(RunContinuationsAsynchronously);
        Stopped  = Finisher.Task;
    }

    InterceptorController  Interceptors      { get; }
    SourceConnectorOptions Options           { get; }
    ILoggerFactory         LoggerFactory     { get; }
    TaskCompletionSource   Finisher          { get; }
    IProducer              Producer          { get; }
    IReader                Reader            { get; }
    ConnectorMetadata      ConnectorMetadata { get; }
    ServiceLocker          ServiceLocker     { get; }
    ISource                Source            { get; }
    ILogger                Logger            { get; }

    ProcessorMetadata ProcessorMetadata => new() {
        ProcessorId      = Options.ConnectorId,
        ClientId         = Options.ConnectorId,
        SubscriptionName = Options.ConnectorId
    };

    public Task           Stopped     { get; }
    public ConnectorId    ConnectorId { get; }
    public ConnectorState State       { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            if (State is ConnectorState.Running or ConnectorState.Activating)
                return;

            await ChangeProcessorState(ProcessorState.Activating);

            await Execute(stoppingToken).ConfigureAwait(false);
            Finisher.SetResult();
        }
        catch (OperationCanceledException) {
            Finisher.SetCanceled(stoppingToken);
        }
        catch (Exception ex) {
            Finisher.SetException(ex);
        }
    }

    async Task Execute(CancellationToken stoppingToken) {
        await ChangeProcessorState(ProcessorState.Running);

        var lockToken = await ServiceLocker.EnsureLocked(
            async (lease, token) => {
                // TBD: Implement
            },
            stoppingToken
        );

        using var connectorLifetime = CancellationTokenSource.CreateLinkedTokenSource(lockToken, stoppingToken);

        var openContext = new SourceOpenContext {
            Metadata          = ConnectorMetadata,
            Configuration     = Options.Configuration,
            ServiceProvider   = Options.ServiceProvider,
            SchemaRegistry    = Options.SchemaRegistry,
            Serializer        = Options.SchemaRegistry,
            Logger            = Logger,
            CancellationToken = connectorLifetime.Token
        };

        try {
            await Source.Open(openContext);
        }
        catch (OperationCanceledException) {
            // ignore
        }
        catch (Exception ex) {
            throw new($"Failed to open sink: {ConnectorMetadata.ConnectorId}", ex);
        }

        try {
            await foreach (var record in Source.Read(connectorLifetime.Token)) {
                var request = ProduceRequest.Builder
                    .Message(
                        Message.Builder
                            .Headers(record.Headers)
                            .RecordId(record.Id)
                            .WithSchema(record.SchemaInfo)
                            .Key(record.Key)
                            .Value(record.Data)
                            .Create()
                    )
                    .Stream(record.Position.StreamId)
                    .Create();

                var result = await Producer.Produce(request, throwOnError: false);

                if (result.Success) {
                    await Source.Track(record);
                }
                else {
                    LogProduceError(Logger, Options.ConnectorId, record.Id, result.Error?.Message);
                }
            }
        }
        catch (OperationCanceledException) {
            await Source.CommitAll();
        }
        catch (Exception ex) {
            LogReadError(Logger, Options.ConnectorId, ex.Message);
            throw;
        }
        finally {
            await DisposeAsyncCore(stoppingToken);
        }
    }

    public async Task Connect(CancellationToken stoppingToken) {
        await ExecuteAsync(stoppingToken).ConfigureAwait(false);
        await Stopped.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsyncCore(CancellationToken ct) {
        if (State is ConnectorState.Running or ConnectorState.Activating)
            await ChangeProcessorState(ProcessorState.Deactivating);

        await Source.CommitAll();

        await ServiceLocker.DisposeAsync();

        await Source.Close(
            new SourceCloseContext {
                Metadata          = ConnectorMetadata,
                Configuration     = Options.Configuration,
                SchemaRegistry    = Options.SchemaRegistry,
                Serializer        = Options.SchemaRegistry,
                Logger            = Logger,
                CancellationToken = ct
            }
        );

        await Producer.DisposeAsync();
        await Reader.DisposeAsync();

        await ChangeProcessorState(ProcessorState.Stopped);
    }

    public async ValueTask DisposeAsync() {
        await StopAsync(CancellationToken.None);
        await DisposeAsyncCore(CancellationToken.None);

        GC.SuppressFinalize(this);
    }

    async Task ChangeProcessorState(ProcessorState state, Exception? error = null) {
        var previousState = (ProcessorState)State;
        State = (ConnectorState)state;

        await Interceptors.Intercept(
            new ProcessorStateChanged(ProcessorMetadata, FromState: previousState, Error: error),
            CancellationToken.None
        );
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Error, Message = "[{ConnectorId}] failed to produce record {RecordId}: {ErrorMessage}")]
    static partial void LogProduceError(ILogger logger, ConnectorId connectorId, RecordId recordId, string? errorMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{ConnectorId}] failed to read records from source: {ErrorMessage}")]
    static partial void LogReadError(ILogger logger, ConnectorId connectorId, string errorMessage);

    [LoggerMessage(Level = LogLevel.Debug, Message = "[{ConnectorId}] Initializing...")]
    static partial void LogInitializing(ILogger logger, ConnectorId connectorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "[{ConnectorId}] Initialized")]
    static partial void LogInitialized(ILogger logger, ConnectorId connectorId);

    [LoggerMessage(Level = LogLevel.Error, Message = "[{ConnectorId}] Failed to initialize")]
    static partial void LogInitializationFailed(ILogger logger, ConnectorId connectorId, Exception ex);

    #endregion
}
