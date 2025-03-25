// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

namespace Kurrent.Surge.Connectors.Sources;

/// <summary>
/// Defines the contract for a sink in the EventStore system.
/// </summary>
public interface ISource {
	/// <summary>
	/// Opens the data source with the provided context.
	/// </summary>
	/// <param name="context">The context containing information needed to open the source.</param>
	ValueTask Open(SourceOpenContext context) => ValueTask.CompletedTask;

    /// <summary>
    /// Reads records from a source. This method is asynchronous, allowing it to perform I/O operations without blocking the calling thread.
    /// </summary>
    IAsyncEnumerable<SurgeRecord> Read(CancellationToken stoppingToken);

    ValueTask Track(SurgeRecord record);
    ValueTask Commit(SurgeRecord record);
    ValueTask CommitAll();

    /// <summary>
    /// Performs any necessary cleanup when the sink is no longer needed.
    /// This method is optional and can be overridden to close resources like file handles or network connections.
    /// </summary>
    /// <param name="context">The context for the sink's operation</param>
    ValueTask Close(SourceCloseContext context) => ValueTask.CompletedTask;
}
