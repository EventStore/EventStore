// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using EventStore.Projections.Core.Messages;
using ILogger = Serilog.ILogger;

namespace EventStore.Projections.Core.Services.Processing.Strategies;

public class ProcessingStrategySelector {
	private readonly ILogger _logger = Serilog.Log.ForContext<ProcessingStrategySelector>();
	private readonly ReaderSubscriptionDispatcher _subscriptionDispatcher;

	public ProcessingStrategySelector(
		ReaderSubscriptionDispatcher subscriptionDispatcher) {
		_subscriptionDispatcher = subscriptionDispatcher;
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
				enableContentTypeValidation)
			: new ContinuousProjectionProcessingStrategy(
				name,
				projectionVersion,
				stateHandler,
				projectionConfig,
				sourceDefinition,
				_logger,
				_subscriptionDispatcher,
				enableContentTypeValidation);
	}
}
