// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using Serilog;

namespace EventStore.Core.Services.Archive.Archiver;

public class ArchiverService :
	IHandle<SystemMessage.ChunkLoaded>,
	IHandle<SystemMessage.ChunkCompleted>,
	IHandle<SystemMessage.ChunkSwitched>,
	IHandle<ReplicationTrackingMessage.ReplicatedTo>,
	IHandle<SystemMessage.SystemStart>,
	IHandle<SystemMessage.BecomeShuttingDown>
{
	private static readonly ILogger Log = Serilog.Log.ForContext<ArchiverService>();

	private readonly ISubscriber _mainBus;
	private readonly IArchiveStorageWriter _archiveWriter;
	private readonly IArchiveStorageReader _archiveReader;
	private readonly Queue<ChunkInfo> _uncommittedChunks;
	private readonly ConcurrentDictionary<string, ChunkInfo> _existingChunks;
	private readonly CancellationTokenSource _cts;
	private readonly Channel<Commands.ArchiveChunk> _archiveChunkCommands;

	private readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);
	private long _replicationPosition;

	public ArchiverService(ISubscriber mainBus, IArchiveStorageFactory archiveStorageFactory) {
		_mainBus = mainBus;
		_archiveWriter = archiveStorageFactory.CreateWriter();
		_archiveReader = archiveStorageFactory.CreateReader();

		_uncommittedChunks = new();
		_existingChunks = new();
		_cts = new();
		_archiveChunkCommands = Channel.CreateUnboundedPrioritized(
			new UnboundedPrioritizedChannelOptions<Commands.ArchiveChunk> {
				SingleWriter = false,
				SingleReader = true,
				Comparer = new ChunkPrioritizer()
			});

		Subscribe();
	}

	private void Subscribe() {
		_mainBus.Subscribe<SystemMessage.ChunkLoaded>(this);
		_mainBus.Subscribe<SystemMessage.ChunkSwitched>(this);
		_mainBus.Subscribe<SystemMessage.ChunkCompleted>(this);
		_mainBus.Subscribe<ReplicationTrackingMessage.ReplicatedTo>(this);
		_mainBus.Subscribe<SystemMessage.SystemStart>(this);
		_mainBus.Subscribe<SystemMessage.BecomeShuttingDown>(this);
	}

	public void Handle(SystemMessage.ChunkLoaded message) {
		if (!message.ChunkInfo.IsCompleted)
			return;

		_existingChunks[Path.GetFileName(message.ChunkInfo.ChunkFileName)!] = message.ChunkInfo;
	}

	public void Handle(SystemMessage.ChunkCompleted message) {
		var chunkInfo = message.ChunkInfo;
		if (chunkInfo.ChunkEndPosition > _replicationPosition) {
			_uncommittedChunks.Enqueue(chunkInfo);
			return;
		}

		ScheduleChunkForArchiving(chunkInfo, "new");
	}

	public void Handle(SystemMessage.ChunkSwitched message) {
		ScheduleChunkForArchiving(message.ChunkInfo, "changed");
	}

	public void Handle(ReplicationTrackingMessage.ReplicatedTo message) {
		_replicationPosition = Math.Max(_replicationPosition, message.LogPosition);
		ProcessUncommittedChunks();
	}

	public void Handle(SystemMessage.SystemStart message) {
		Task.Run(async () => {
			await ScheduleExistingChunksForArchiving(_cts.Token);
			await ArchiveChunks(_cts.Token);
		}, _cts.Token);
	}

	public void Handle(SystemMessage.BecomeShuttingDown message) {
		try {
			_cts.Cancel();
		} catch {
			// ignore
		} finally {
			_cts?.Dispose();
		}
	}

	private void ProcessUncommittedChunks() {
		while (_uncommittedChunks.TryPeek(out var chunkInfo)) {
			if (chunkInfo.ChunkEndPosition > _replicationPosition)
				break;

			_uncommittedChunks.Dequeue();
			ScheduleChunkForArchiving(chunkInfo, "new");
		}
	}

	private void ScheduleChunkForArchiving(ChunkInfo chunkInfo, string chunkType) {
		var writeResult = _archiveChunkCommands.Writer.TryWrite(new Commands.ArchiveChunk {
			ChunkPath = chunkInfo.ChunkFileName,
			ChunkStartNumber = chunkInfo.ChunkStartNumber,
			ChunkEndNumber = chunkInfo.ChunkEndNumber
		});

		Debug.Assert(writeResult); // writes should never fail as the channel's length is unbounded

		Log.Information("Scheduled archiving of {chunkFile} ({chunkType})",
			Path.GetFileName(chunkInfo.ChunkFileName), chunkType);
	}

	private async Task ArchiveChunks(CancellationToken ct) {
		try {
			await foreach (var cmd in _archiveChunkCommands.Reader.ReadAllAsync(ct)) {
				await ArchiveChunk(cmd.ChunkPath, cmd.ChunkStartNumber, cmd.ChunkEndNumber, ct);
			}
		} catch (OperationCanceledException) {
			// ignore
		} catch (Exception ex) {
			Log.Fatal(ex, "Archiving has stopped working due to an unhandled exception.");
		}
	}

	private async Task ArchiveChunk(string chunkPath, int chunkStartNumber, int chunkEndNumber, CancellationToken ct) {
		var chunkFile = Path.GetFileName(chunkPath);
		try {
			Log.Information("Archiving {chunkFile}", chunkFile);

			while (!await _archiveWriter.StoreChunk(chunkPath, ct)) {
				Log.Warning("Archiving of {chunkFile} failed. Retrying in: {retryInterval}.", chunkFile, RetryInterval);
				await Task.Delay(RetryInterval, ct);
			}

			while (!await _archiveWriter.RemoveChunks(chunkStartNumber, chunkEndNumber, chunkFile, ct)) {
				Log.Warning(
					"Clean up of old chunks: {chunkStartNumber}-{chunkEndNumber} failed. Retrying in: {retryInterval}.",
					chunkStartNumber, chunkEndNumber, RetryInterval);
				await Task.Delay(RetryInterval, ct);
			}

			Log.Information("Archiving of {chunkFile} succeeded.", chunkFile);
		} catch (ChunkDeletedException) {
			// the chunk has been deleted, presumably during scavenge or redaction
			Log.Information("Archiving of {chunkFile} cancelled as it was deleted.", Path.GetFileName(chunkPath));
		} catch (OperationCanceledException) {
			throw;
		} catch (Exception ex) {
			Log.Error(ex, "Archiving of {chunkFile} failed.", chunkFile);
			throw;
		}
	}

	private async Task ScheduleExistingChunksForArchiving(CancellationToken ct) {
		try {
			await foreach (var archivedChunk in _archiveReader.ListChunks(ct))
				_existingChunks.Remove(archivedChunk, out _);

			Log.Information("Scheduling archiving of {numChunks} existing chunks.", _existingChunks.Count);
			foreach (var chunkInfo in _existingChunks.Values)
				ScheduleChunkForArchiving(chunkInfo, "old");

			_existingChunks.Clear();
		} catch (OperationCanceledException) {
			// ignore
		} catch (Exception ex) {
			Log.Error(ex, "Archiving of existing chunks has stopped due to an unhandled exception.");
		}
	}
}
