// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.POC.ConnectorsEngine.Infrastructure;

namespace EventStore.POC.ConnectorsEngine;

public static class Events {
	public record ConnectorCreated(
		string Filter,
		string Sink,
		NodeState Affinity,
		int CheckpointInterval) : Message;

	public record ConnectorEnabled : Message;

	public record ConnectorDisabled : Message;

	public record ConnectorDeleted : Message;
	
	public record ConnectorReset(ulong? CommitPosition, ulong? PreparePosition) : Message;

	public record ConnectorRegistered(string ConnectorId) : Message;

	public record ConnectorDeregistered(string ConnectorId) : Message;

}
