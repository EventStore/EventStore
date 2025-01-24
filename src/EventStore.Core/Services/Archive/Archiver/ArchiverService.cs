// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Runtime.CompilerServices;
using DotNext.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;

namespace EventStore.Core.Services.Archive.Archiver;

public sealed class ArchiverService :
	IHandle<SystemMessage.ChunkSwitched>,
	IHandle<ReplicationTrackingMessage.ReplicatedTo>,
	IHandle<SystemMessage.SystemStart>,
	IHandle<SystemMessage.BecomeShuttingDown>,
	IAsyncDisposable
{
	private readonly IArchiveStorage _archive;
	private readonly CancellationToken _lifetimeToken;
	private readonly AsyncAutoResetEvent _archivingSignal;
	private readonly IChunkRegistry<IChunkBlob> _chunkManager;
	private readonly ConcurrentBag<ChunkInfo> _switchedChunks;
	private Task _archivingTask;

	private long _replicationPosition; // volatile
	private volatile CancellationTokenSource _cts;

	// systemStart
	public ArchiverService(
		ISubscriber mainBus,
		IArchiveStorage archiveStorage,
		IChunkRegistry<IChunkBlob> chunkChunkManager) {
		_archive = archiveStorage;
		_cts = new();
		_lifetimeToken = _cts.Token;
		_archivingSignal = new(initialState: false);
		_chunkManager = chunkChunkManager;
		_archivingTask = Task.CompletedTask;
		_switchedChunks = new();

		mainBus.Subscribe<SystemMessage.ChunkSwitched>(this);
		mainBus.Subscribe<ReplicationTrackingMessage.ReplicatedTo>(this);
		mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(this);
	}

	public void Handle(SystemMessage.SystemStart message) {
		_archivingTask = ArchiveAsync();
	}

	public void Handle(SystemMessage.ChunkSwitched message) {
		_switchedChunks.Add(message.ChunkInfo);
		_archivingSignal.Set();
	}

	public void Handle(ReplicationTrackingMessage.ReplicatedTo message) {
		Atomic.AccumulateAndGet(ref _replicationPosition, message.LogPosition, long.Max);
		_archivingSignal.Set();
	}

	[AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
	private async Task ArchiveAsync() {
		var checkpoint = await _archive.GetCheckpoint(_lifetimeToken);
		while (!_lifetimeToken.IsCancellationRequested) {
			await ProcessSwitchedChunksAsync(checkpoint, _lifetimeToken);
			var chunk = _chunkManager.GetChunkFor(checkpoint);
			if (chunk.ChunkFooter is { IsCompleted: true } &&
			    chunk.ChunkHeader.ChunkEndPosition <= Volatile.Read(in _replicationPosition)) {
				await _archive.StoreChunk(chunk, _lifetimeToken);
				checkpoint = chunk.ChunkHeader.ChunkEndPosition;
				await _archive.SetCheckpoint(checkpoint, _lifetimeToken);
			} else {
				await _archivingSignal.WaitAsync(_lifetimeToken);
			}
		}
	}

	private async ValueTask ProcessSwitchedChunksAsync(long checkpoint, CancellationToken token) {
		// process only chunks that are behind of the checkpoint, all other chunks
		// will be processed by the main loop
		while (_switchedChunks.TryTake(out var chunkInfo)) {
			if (chunkInfo.ChunkEndNumber < checkpoint) {
				var chunk = _chunkManager.GetChunk(chunkInfo.ChunkEndNumber);
				await _archive.StoreChunk(chunk, token);
			}
		}
	}

	public void Handle(SystemMessage.BecomeShuttingDown message) {
		Cancel();
	}

	private void Cancel() {
		if (Interlocked.Exchange(ref _cts, null) is { } cts) {
			using (cts) {
				cts.Cancel();
			}
		}
	}

	public async ValueTask DisposeAsync() {
		Cancel();
		try {
			await _archivingTask.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext |
			                                    ConfigureAwaitOptions.SuppressThrowing);
		} finally {
			_archivingSignal.Dispose();
		}
	}
}
