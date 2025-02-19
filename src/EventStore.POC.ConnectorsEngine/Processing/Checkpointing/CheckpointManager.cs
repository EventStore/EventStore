// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;
using EventStore.POC.IO.Core;
using Serilog;

namespace EventStore.POC.ConnectorsEngine.Processing.Checkpointing;

public interface ICheckpointManager {
	public Task<Position?> Load(CancellationToken cancellationToken);
	Task ProcessedEvent(Event @event, CancellationToken cancellationToken);
}

public class ConnectorCheckpointManager : ICheckpointManager {
	private readonly string _checkpointStreamName;
	private readonly ILogger _logger;
	private readonly CheckpointConfig _config;
	private readonly IClient _client;
	private readonly string _connectorId;
	private readonly ISerializer _serializer;
	private readonly ResetPoint _resetTo;

	private long _eventsProcessedSinceCheckpoint = 0;
	private long _expectedVersion = ExpectedVersion.Any; //qq todo: we can put the event number in the ContextMessage and get it on load
	// we remember what round we are in when we load, so that we can save.
	private ulong _round;

	public ConnectorCheckpointManager(
		string connectorId,
		CheckpointConfig config,
		ResetPoint resetTo,
		INamingStrategy namingStrategy,
		IClient client,
		ISerializer serializer,
		ILogger logger) {

		_config = config;
		_resetTo = resetTo;
		_round = resetTo.Round;
		_connectorId = connectorId;
		_checkpointStreamName = namingStrategy.NameFor(connectorId);
		_client = client;
		_serializer = serializer;
		_logger = logger;
	}

	private IAsyncEnumerable<Message> ReadStreamBackwards(CancellationToken cancellationToken) => _client
		.ReadStreamBackwards(_checkpointStreamName, 1, cancellationToken)
		.HandleStreamNotFound()
		.DeserializeSkippingUnknown(_serializer);

	public async Task<Position?> Load(CancellationToken cancellationToken) {
		_logger.Information("Loading checkpoint for {connector} from {checkpointStreamName}", _connectorId, _checkpointStreamName);
		await foreach (var evnt in ReadStreamBackwards(cancellationToken)) {
			if (evnt is Messages.AllCheckpointed checkpointed) {
				var checkpoint = new ResetPoint(
					checkpointed.Round,
					new(checkpointed.CommitPosition, checkpointed.PreparePosition));

				if (checkpoint < _resetTo) {
					_logger.Information("Found checkpoint {checkpoint} for connector {connector} but starting from reset {resetTo}",
						checkpoint, _connectorId, _resetTo);
					return _resetTo.Checkpoint;
				}

				_logger.Information("Found checkpoint {checkpoint} for connector {connector}",
					checkpoint, _connectorId);
				_round = checkpoint.Round;
				return checkpoint.Checkpoint;
			}
		}

		_logger.Information("No existing checkpoint for {connector}. Starting from reset {resetTo}",
			_connectorId, _resetTo);

		// Creating metadata for the connector-checkpoint
		await _client.WriteMetaDataMaxCountAsync(_checkpointStreamName, cancellationToken);
		return _resetTo.Checkpoint;
	}

	public async Task ProcessedEvent(Event @event, CancellationToken cancellationToken) {
		_eventsProcessedSinceCheckpoint++;
		if (_config.Interval > 0 && _eventsProcessedSinceCheckpoint >= _config.Interval) {
			var checkpoint = new Position(@event.CommitPosition, @event.PreparePosition);
			_logger.Verbose("Connector {connectorId} processed {processed} events, checkpointing at {checkpoint}",
				_connectorId, _eventsProcessedSinceCheckpoint, checkpoint);

			var res = await _client.WriteAsync(
				_checkpointStreamName,
				new[] { _serializer.Serialize(new Messages.AllCheckpointed(
					_round,
					checkpoint.CommitPosition,
					checkpoint.PreparePosition))
				},
				_expectedVersion,
				cancellationToken);
			_expectedVersion = res;
			_eventsProcessedSinceCheckpoint = 0;
		}
	}
}
