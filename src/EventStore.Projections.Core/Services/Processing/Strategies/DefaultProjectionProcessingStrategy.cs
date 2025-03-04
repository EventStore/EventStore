// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing.Checkpointing;
using EventStore.Projections.Core.Services.Processing.Emitting;
using EventStore.Projections.Core.Services.Processing.Partitioning;
using EventStore.Projections.Core.Services.Processing.Phases;
using ILogger = Serilog.ILogger;

namespace EventStore.Projections.Core.Services.Processing.Strategies;

public abstract class DefaultProjectionProcessingStrategy : EventReaderBasedProjectionProcessingStrategy {
	private readonly IProjectionStateHandler _stateHandler;

	protected DefaultProjectionProcessingStrategy(
		string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
		ProjectionConfig projectionConfig, IQuerySources sourceDefinition, ILogger logger,
		ReaderSubscriptionDispatcher subscriptionDispatcher, bool enableContentTypeValidation, int maxProjectionStateSize)
		: base(name, projectionVersion, projectionConfig, sourceDefinition, logger, subscriptionDispatcher,
			enableContentTypeValidation, maxProjectionStateSize) {
		_stateHandler = stateHandler;
	}

	protected override IProjectionProcessingPhase CreateFirstProcessingPhase(
		IPublisher publisher,
		IPublisher inputQueue,
		Guid projectionCorrelationId,
		PartitionStateCache partitionStateCache,
		Action updateStatistics,
		CoreProjection coreProjection,
		ReaderSubscriptionDispatcher subscriptionDispatcher,
		CheckpointTag zeroCheckpointTag,
		ICoreProjectionCheckpointManager checkpointManager,
		IReaderStrategy readerStrategy,
		IResultWriter resultWriter,
		IEmittedStreamsTracker emittedStreamsTracker) {
		var statePartitionSelector = CreateStatePartitionSelector();

		var orderedPartitionProcessing = _sourceDefinition.ByStreams && _sourceDefinition.IsBiState;
		return new EventProcessingProjectionProcessingPhase(
			coreProjection,
			projectionCorrelationId,
			publisher,
			inputQueue,
			_projectionConfig,
			updateStatistics,
			_stateHandler,
			partitionStateCache,
			_sourceDefinition.DefinesStateTransform,
			_name,
			_logger,
			zeroCheckpointTag,
			checkpointManager,
			statePartitionSelector,
			subscriptionDispatcher,
			readerStrategy,
			resultWriter,
			_projectionConfig.CheckpointsEnabled,
			this.GetStopOnEof(),
			_sourceDefinition.IsBiState,
			orderedPartitionProcessing: orderedPartitionProcessing,
			emittedStreamsTracker: emittedStreamsTracker,
			enableContentTypeValidation: _enableContentTypeValidation);
	}

	protected virtual StatePartitionSelector CreateStatePartitionSelector() {
		return _sourceDefinition.ByCustomPartitions
			? new ByHandleStatePartitionSelector(_stateHandler)
			: (_sourceDefinition.ByStreams
				? (StatePartitionSelector)new ByStreamStatePartitionSelector()
				: new NoopStatePartitionSelector());
	}
}
