// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;
using Serilog;

namespace EventStore.Core.Services.Storage;

// The resulting scavenger is used for one continuous run. If it is cancelled or
// completed then starting scavenge again will instantiate another scavenger
// with a different id.
public class ScavengerFactory {
	private readonly Func<ClientMessage.ScavengeDatabase, ITFChunkScavengerLog, ILogger, IScavenger> _create;

	public ScavengerFactory(Func<ClientMessage.ScavengeDatabase, ITFChunkScavengerLog, ILogger, IScavenger> create) {
		_create = create;
	}

	public IScavenger Create(ClientMessage.ScavengeDatabase message, ITFChunkScavengerLog scavengerLogger, ILogger logger) =>
		_create(message, scavengerLogger, logger);
}

public class OldScavenger<TStreamId> : IScavenger {
	private readonly bool _alwaysKeepScavenged;
	private readonly bool _mergeChunks;
	private readonly int _startFromChunk;
	private readonly TFChunkScavenger<TStreamId> _tfChunkScavenger;

	public string ScavengeId => _tfChunkScavenger.ScavengeId;

	public OldScavenger(
		bool alwaysKeepScaveged,
		bool mergeChunks,
		int startFromChunk,
		TFChunkScavenger<TStreamId> tfChunkScavenger) {

		_alwaysKeepScavenged = alwaysKeepScaveged;
		_mergeChunks = mergeChunks;
		_startFromChunk = startFromChunk;
		_tfChunkScavenger = tfChunkScavenger;
	}

	public void Dispose() {
	}

	public Task<ScavengeResult> ScavengeAsync(CancellationToken cancellationToken) {
		return _tfChunkScavenger.Scavenge(
			alwaysKeepScavenged: _alwaysKeepScavenged,
			mergeChunks: _mergeChunks,
			startFromChunk: _startFromChunk,
			ct: cancellationToken);
	}
}
