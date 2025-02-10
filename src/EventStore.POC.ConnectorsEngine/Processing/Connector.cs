// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.POC.ConnectorsEngine.Processing.Checkpointing;
using EventStore.POC.IO.Core;
using Serilog;

namespace EventStore.POC.ConnectorsEngine.Processing;

public interface IConnector : IDisposable {
	Task RunAsync();
}

public sealed class Connector : IConnector {
	private readonly string _id;
	private readonly IFilter _filter;
	private readonly IClient _client;
	private readonly ISink _sink;
	private readonly ILogger _logger;
	private readonly ICheckpointManager _checkpointManager;

	private CancellationTokenSource? _cts;

	public Connector(string id, IFilter filter, IClient client, ISink sink, ILogger logger,
		ICheckpointManager checkpointManager) {

		_id = id;
		_filter = filter;
		_client = client;
		_sink = sink;
		_logger = logger;
		_checkpointManager = checkpointManager;

		_cts = null;
	}

	public void Dispose() {
		_cts?.Cancel();
	}

	public async Task RunAsync() {
		try {
			_cts = new CancellationTokenSource();

			var checkpoint = await _checkpointManager.Load(_cts.Token);

			var start = checkpoint is null
				? FromAll.Start
				: FromAll.After(new Position(
					checkpoint.Value.CommitPosition,
					checkpoint.Value.PreparePosition));

			if (checkpoint is null) {
				_logger.Information("Connector({id}): SUB START FROM BEGINNING", _id);
			} else {
				_logger.Information("Connector({id}): SUB START FROM CHECKPOINT {checkpoint}", _id, checkpoint);
			}

			await ConsumeAsync(_client.SubscribeToAll(start, _cts.Token), _cts.Token);

		} catch (Exception ex) when (ex is not OperationCanceledException) {
			//qq restart? at what point should we require human intervention.
			_logger.Error("Connector({id}): EXCEPTION: {exception}", _id, ex);
		} finally {
			_logger.Information("Connector({id}): SUB STOPPED", _id);
		}
	}

	private async Task ConsumeAsync(IAsyncEnumerable<Event> events, CancellationToken cancellationToken) {
		await foreach (var evt in events) {
			if (evt.EventType.StartsWith("$"))
				continue; //qq be careful about checkpoints counting towards triggering checkpoints

			if (_filter.Evaluate(evt))
				await _sink.Write(evt, cancellationToken);
			await _checkpointManager.ProcessedEvent(evt, cancellationToken);
		}
	}
}
