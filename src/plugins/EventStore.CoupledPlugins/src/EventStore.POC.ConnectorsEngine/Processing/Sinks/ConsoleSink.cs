// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.POC.IO.Core;
using Serilog;

namespace EventStore.POC.ConnectorsEngine.Processing.Sinks;

public class ConsoleSink : ISink {
	private readonly string _connectorId;
	private readonly ILogger _logger;

	public ConsoleSink(string connectorId, ILogger logger) {
		_connectorId = connectorId;
		_logger = logger;
	}

	public Uri Config { get; } = new("console://");

	public Task Write(Event e, CancellationToken ct) {
		_logger.Information("ConsoleSink: {id} Received an event: {stream} : {event}", _connectorId, e.Stream, e.EventType);
		return Task.CompletedTask;
	}
}
