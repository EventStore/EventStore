// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.IO.Core;

namespace EventStore.POC.ConnectorsEngine.Management;

public enum Status {
	None,
	Stopped,
	Enabled,
}

public class ConnectorManager : Aggregate<ConnectorManagerState> {
	public void Create(
		string connectorId,
		string filter,
		string sink,
		NodeState affinity,
		bool enable,
		int checkpointInterval) {

		if (State.Status != Status.None) {
			if (connectorId == State.Id &&
				filter == State.Filter &&
				sink == State.Sink &&
				affinity == State.Affinity &&
				enable == (State.Status == Status.Enabled) &&
				checkpointInterval == State.CheckpointInterval)

				return; // idempotent

			throw new CommandException($"Connector already exists");
		}

		if (string.IsNullOrWhiteSpace(connectorId))
			throw new CommandException($"Name is null or white space");

		if (string.IsNullOrWhiteSpace(sink))
			throw new CommandException($"Sink is null or white space");

		if (affinity == NodeState.Unmapped)
			throw new CommandException($"Affinity was not recognised");

		try {
			_ = new UriBuilder(sink);
		} catch (UriFormatException ex) {
			throw new CommandException($"Invalid sink URI: {ex.Message}");
		}

		Append(new Events.ConnectorCreated(
			Filter: filter,
			Sink: sink,
			Affinity: affinity,
			CheckpointInterval: checkpointInterval));

		Append(new Events.ConnectorReset(CommitPosition: null, PreparePosition: null));

		if (enable)
			Enable();
	}

	public void Enable() {
		if (State.Status == Status.None)
			throw new CommandException($"Connector does not exist");

		if (State.Status == Status.Enabled)
			return; // idempotent

		Append(new Events.ConnectorEnabled());
	}

	public void Disable() {
		if (State.Status == Status.None)
			throw new CommandException($"Connector does not exist");

		if (State.Status == Status.Stopped)
			return; // idempotent

		Append(new Events.ConnectorDisabled());
	}
	
	public async Task Reset(
		ulong? commitPosition, ulong? preparePosition, IClient client, CancellationToken ct) {

		if (State.Status == Status.None)
			throw new CommandException($"Connector does not exist");

		if (commitPosition is not null || preparePosition is not null) {
			var position = new Position(
				commitPosition ?? 0,
				preparePosition ?? 0);

			try {
				await foreach (var evt in client.ReadAllBackwardsAsync(position, maxCount: 1, ct))
					;
			} catch (Exception ex) {
				//qq differentiate between actual invalid positions and connection errors
				// consider retries
				throw new CommandException($"Invalid Position. {ex.Message}");
			}
		}

		Append(new Events.ConnectorReset(commitPosition, preparePosition));
	}

	public void Delete() {
		if (State.Status == Status.None)
			return; // idempotent

		if (State.Status == Status.Enabled)
			Disable();

		Append(new Events.ConnectorDeleted());
	}
}

public class ConnectorManagerState : State {
	public Status Status { get; private set; } = Status.None;
	public string Filter { get; private set; } = "";
	public string Sink { get; private set; } = "";
	public NodeState Affinity { get; private set; }
	public int CheckpointInterval { get; private set; }

	public ConnectorManagerState() {
		Register<Events.ConnectorCreated>(msg => {
			Status = Status.Stopped;
			Filter = msg.Filter;
			Sink = msg.Sink;
			Affinity = msg.Affinity;
			CheckpointInterval = msg.CheckpointInterval;
		});

		Register<Events.ConnectorEnabled>(_ => {
			Status = Status.Enabled;
		});

		Register<Events.ConnectorDisabled>(_ => {
			Status = Status.Stopped;
		});

		Register<Events.ConnectorDeleted>(_ => {
			Status = Status.None;
		});
	}
}
