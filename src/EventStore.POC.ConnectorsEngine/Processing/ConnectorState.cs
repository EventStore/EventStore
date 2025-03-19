// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.POC.ConnectorsEngine.Processing.Checkpointing;

namespace EventStore.POC.ConnectorsEngine.Processing;

public record ConnectorState(
	string Id,
	string Filter,
	string Sink,
	NodeState Affinity = NodeState.Leader,
	bool Enabled = true,
	ResetPoint ResetTo = new(),
	CheckpointConfig? CheckpointConfig = null);
