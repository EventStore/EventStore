// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using EventStore.Core.Messages;

namespace EventStore.Core.TransactionLog.Scavenging;

public class ScavengeOptionsCalculator {
	readonly ClusterVNodeOptions _vNodeOptions;
	readonly ClientMessage.ScavengeDatabase _message;

	public ScavengeOptionsCalculator(
		ClusterVNodeOptions vNodeOptions,
		ClientMessage.ScavengeDatabase message) {

		_vNodeOptions = vNodeOptions;
		_message = message;
	}

	public bool MergeChunks => !_vNodeOptions.Database.DisableScavengeMerging;

	public int ChunkExecutionThreshold => _message.Threshold ?? 0;
}
