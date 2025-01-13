// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Naming;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using Serilog;

namespace EventStore.Core.Services.Archive.ArchiveCatchup;

// The archive catchup process downloads chunks that are missing locally from the archive.
//
// This is needed in some cases:
// i)  a follower may be far behind the leader and the latter may have already deleted archived chunks locally.
// ii) under normal circumstances, a leader should never need to catch up from the archive. however, if a cluster's
//     data was restored from a backup, we can end up in a situation where the leader-to-be is behind the archive.
//     in this case, we still want all nodes to catch up with the archive *before* joining the cluster to maintain
//     consistency between the data that's in the cluster and in the archive.

public class ArchiveCatchup : IClusterVNodeStartupTask {
	private readonly string _dbPath;
	private readonly ICheckpoint _writerCheckpoint;
	private readonly ICheckpoint _replicationCheckpoint;
	private readonly int _chunkSize;
	private readonly IArchiveStorageReader _archiveReader;
	private readonly IArchiveChunkNameResolver _chunkNameResolver;

	private static readonly ILogger Log = Serilog.Log.ForContext<ArchiveCatchup>();
	private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);

	public ArchiveCatchup(
		string dbPath,
		ICheckpoint writerCheckpoint,
		ICheckpoint replicationCheckpoint,
		int chunkSize,
		IArchiveStorageFactory archiveStorageFactory,
		IArchiveChunkNameResolver chunkNameResolver) {
		_dbPath = dbPath;
		_writerCheckpoint = writerCheckpoint;
		_replicationCheckpoint = replicationCheckpoint;
		_chunkSize = chunkSize;
		_archiveReader = archiveStorageFactory.CreateReader();
		_chunkNameResolver = chunkNameResolver;
	}

	public Task Run() => Run(CancellationToken.None);

	private async Task Run(CancellationToken ct) {
		var writerChk = _writerCheckpoint.Read();
		var archiveChk = await GetArchiveCheckpoint(ct);

		if (writerChk >= archiveChk)
			return;

		Log.Information("Catching up with the archive. Writer checkpoint: 0x{writerCheckpoint:X}, Archive checkpoint: 0x{archiveCheckpoint:X}.",
			writerChk, archiveChk);

		while (!await CatchUpWithArchive(writerChk, archiveChk, ct))
			writerChk = _writerCheckpoint.Read();
	}

	// returns true if the catchup is done
	// returns false if it needs to be invoked again to continue the catchup
	private async Task<bool> CatchUpWithArchive(long writerChk, long archiveChk, CancellationToken ct) {
		var logicalChunkStartNumber = (int) (writerChk / _chunkSize);
		var logicalChunkEndNumber = (int) (archiveChk / _chunkSize);

		for (var logicalChunkNumber = logicalChunkStartNumber; logicalChunkNumber < logicalChunkEndNumber; logicalChunkNumber++)
			if (!await FetchAndCommitChunk(logicalChunkNumber, ct))
				return false;

		Log.Information("Catch-up with the archive completed");
		return true;
	}

	private async Task<long> GetArchiveCheckpoint(CancellationToken ct) {
		do {
			try {
				return await _archiveReader.GetCheckpoint(ct);
			} catch (Exception ex) {
				Log.Error(ex, "Failed to get archive checkpoint. Retrying in: {interval}", RetryInterval);
				await Task.Delay(RetryInterval, ct);
			}
		} while (true);
	}

	private async Task<bool> FetchAndCommitChunk(int logicalChunkNumber, CancellationToken ct) {
		var destinationFile = _chunkNameResolver.ResolveFileName(logicalChunkNumber);
		var destinationPath = Path.Combine(_dbPath, destinationFile);
		if (!await FetchChunk(logicalChunkNumber, destinationPath, ct))
			return false;

		await CommitChunk(destinationPath, ct);
		return true;
	}

	private async Task<bool> FetchChunk(int logicalChunkNumber, string destinationPath, CancellationToken ct) {
		try {
			Log.Information("Fetching chunk: {logicalChunkNumber} from the archive", logicalChunkNumber);

			var tempPath = Path.Combine(_dbPath, Guid.NewGuid() + ".archive.tmp");

			//qqqq now actually needs an IChunkFileSystem
			//await using (var inputStream = await _archiveReader.GetChunk(logicalChunkNumber, ct)) {
			//	await using var outputStream = File.Open(
			//		path: tempPath,
			//		options: new FileStreamOptions {
			//			Mode = FileMode.CreateNew,
			//			Access = FileAccess.ReadWrite,
			//			Share = FileShare.None,
			//			Options = FileOptions.Asynchronous,
			//			PreallocationSize = _chunkSize
			//		});

			//	await inputStream.CopyToAsync(outputStream, ct);
			//}

			if (File.Exists(destinationPath)) {
				var backupPath = $"{destinationPath}.archive.bkup";
				Log.Information("Backing up {chunk} to {chunkBackup}", Path.GetFileName(destinationPath), Path.GetFileName(backupPath));
				File.Move(destinationPath, backupPath, overwrite: true);
			}

			File.Move(tempPath, destinationPath);

			return true;
		} catch (ChunkDeletedException) {
			Log.Warning("Failed to fetch chunk: {logicalChunkNumber} from the archive as it was deleted. This can happen if the archive is being scavenged.", logicalChunkNumber);
			return false;
		} catch (Exception ex) {
			Log.Error(ex, "Failed to fetch chunk: {logicalChunkNumber} from the archive. Retrying in {interval}", logicalChunkNumber, RetryInterval);
			await Task.Delay(RetryInterval, ct);
			return false;
		}
	}

	private async Task CommitChunk(string chunkPath, CancellationToken ct) {
		await using var headerStream = File.OpenRead(chunkPath);
		var header = await ChunkHeader.FromStream(headerStream, ct);

		_writerCheckpoint.Write(header.ChunkEndPosition);
		_writerCheckpoint.Flush();
		Log.Debug("Moved {checkpoint} checkpoint forward to: 0x{position:X}", _writerCheckpoint.Name, header.ChunkEndPosition);

		_replicationCheckpoint.Write(header.ChunkEndPosition);
		_replicationCheckpoint.Flush();
		Log.Debug("Moved {checkpoint} checkpoint forward to: 0x{position:X}", _replicationCheckpoint.Name, header.ChunkEndPosition);
	}
}
