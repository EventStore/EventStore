// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Messages;
using ILogger = Serilog.ILogger;

namespace EventStore.Projections.Core.Services.Processing.Strategies;

public class ProcessingStrategySelector {
	private readonly ILogger _logger = Serilog.Log.ForContext<ProcessingStrategySelector>();
	private readonly ReaderSubscriptionDispatcher _subscriptionDispatcher;
	private readonly int _maxProjectionStateSize;

	public ProcessingStrategySelector(
		ReaderSubscriptionDispatcher subscriptionDispatcher, int maxProjectionStateSize) {
		_subscriptionDispatcher = subscriptionDispatcher;
		_maxProjectionStateSize = maxProjectionStateSize;
	}

	public ProjectionProcessingStrategy CreateProjectionProcessingStrategy(
		string name,
		ProjectionVersion projectionVersion,
		ProjectionNamesBuilder namesBuilder,
		IQuerySources sourceDefinition,
		ProjectionConfig projectionConfig,
		IProjectionStateHandler stateHandler, string handlerType, string query, bool enableContentTypeValidation) {

		return projectionConfig.StopOnEof
			? (ProjectionProcessingStrategy)
			new QueryProcessingStrategy(
				name,
				projectionVersion,
				stateHandler,
				projectionConfig,
				sourceDefinition,
				_logger,
				_subscriptionDispatcher,
				enableContentTypeValidation,
				_maxProjectionStateSize)
			: new ContinuousProjectionProcessingStrategy(
				name,
				projectionVersion,
				stateHandler,
				projectionConfig,
				sourceDefinition,
				_logger,
				_subscriptionDispatcher,
				enableContentTypeValidation,
				_maxProjectionStateSize);
	}
}
