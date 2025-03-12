// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.POC.ConnectorsEngine.Infrastructure;

namespace EventStore.POC.ConnectorsEngine;

public static class Commands {
	public record CreateConnector(
		string ConnectorId,
		string Filter,
		string Sink,
		NodeState Affinity = NodeState.Leader,
		bool Enable = true,
		int checkpointInterval = 10)
		: Message;

	public record ResetConnector(
		string ConnectorId,
		ulong? CommitPosition,
		ulong? PreparePosition) : Message;
}
