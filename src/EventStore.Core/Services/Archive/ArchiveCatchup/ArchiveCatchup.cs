// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.Services.Archive.Storage.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
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
	private readonly IVersionedFileNamingStrategy _fileNamingStrategy;
	private readonly IArchiveStorageReader _archiveReader;

	private static readonly ILogger Log = Serilog.Log.ForContext<ArchiveCatchup>();
	private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);

	public ArchiveCatchup(
		string dbPath,
		ICheckpoint writerCheckpoint,
		ICheckpoint replicationCheckpoint,
		int chunkSize,
		IVersionedFileNamingStrategy fileNamingStrategy,
		IArchiveStorageFactory archiveStorageFactory) {
		_dbPath = dbPath;
		_writerCheckpoint = writerCheckpoint;
		_replicationCheckpoint = replicationCheckpoint;
		_chunkSize = chunkSize;
		_fileNamingStrategy = fileNamingStrategy;
		_archiveReader = archiveStorageFactory.CreateReader();
	}

	public Task Run() => Run(CancellationToken.None);

	private async Task Run(CancellationToken ct) {
		var writerChk = _writerCheckpoint.Read();
		var archiveChk = await GetArchiveCheckpoint(ct);

		if (writerChk >= archiveChk)
			return;

		Log.Information("Catching up with the archive. Writer checkpoint: 0x{writerCheckpoint:X}, Archive checkpoint: 0x{archiveCheckpoint:X}.",
			writerChk, archiveChk);

		while (!await CatchUpWithArchive(writerChk, ct))
			writerChk = _writerCheckpoint.Read();
	}

	private async Task<bool> CatchUpWithArchive(long writerChk, CancellationToken ct) {
		string previousChunk = null;
		var firstChunksToFetch = new List<string>(capacity: 2);

		await using var enumerator = _archiveReader.ListChunks(ct).GetAsyncEnumerator(ct);

		// after this loop, the enumerator will be positioned just after the first chunk in the archive that starts
		// at or after the writer checkpoint
		while (await enumerator.MoveNextAsync()) {
			var chunk = enumerator.Current;
			var chunkStartPos = CalcChunkStartPosition(chunk);

			if (chunkStartPos == writerChk) {
				firstChunksToFetch.Add(chunk);
				break;
			}

			if (chunkStartPos > writerChk) {
				firstChunksToFetch.Add(previousChunk);
				firstChunksToFetch.Add(chunk);
				break;
			}

			previousChunk = chunk;
		}

		// we have gone through all the chunks in the archive but could not find one that starts at or after the writer
		// checkpoint. this case can happen when the database is (less than) one chunk behind the archive.
		// we already know that we are behind the archive as we've compared the checkpoints at the beginning, so there
		// must be at least one chunk to fetch from the archive: the last chunk.
		if (firstChunksToFetch.Count == 0) {
			if (previousChunk == null) {
				// `previousChunk` cannot be null, there must be at least one chunk in the archive
				// (we would not be here otherwise: we cannot be behind the archive if it is empty)
				throw new Exception("There are no chunks in the archive");
			}

			firstChunksToFetch.Add(previousChunk);
		}

		// fetch the first one or two chunks
		foreach(var chunk in firstChunksToFetch)
			if (!await FetchAndCommitChunk(chunk, ct))
				return false;

		// all the remaining chunks are definitely after the writer checkpoint
		while (await enumerator.MoveNextAsync())
			if (!await FetchAndCommitChunk(enumerator.Current, ct))
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

	private long CalcChunkStartPosition(string chunk) {
		var chunkNumber = _fileNamingStrategy.GetIndexFor(chunk);
		return (long) chunkNumber * _chunkSize;
	}

	private async Task<bool> FetchAndCommitChunk(string chunkFile, CancellationToken ct) {
		var chunkPath = Path.Combine(_dbPath, chunkFile);

		if (!await FetchChunk(chunkFile, chunkPath, ct))
			return false;

		await CommitChunk(chunkPath);
		return true;
	}

	private async Task<bool> FetchChunk(string chunkFile, string destinationPath, CancellationToken ct) {
		try {
			Log.Information("Fetching {chunk} from the archive", chunkFile);

			await using var inputStream = await _archiveReader.GetChunk(chunkFile, ct);

			var tempPath = Path.Combine(_dbPath, Guid.NewGuid() + ".archive.tmp");
			await using var outputStream = File.Open(
				path: tempPath,
				options: new FileStreamOptions {
					Mode = FileMode.CreateNew,
					Access = FileAccess.ReadWrite,
					Share = FileShare.None,
					Options = FileOptions.Asynchronous,
					PreallocationSize = _chunkSize
				});

			await inputStream.CopyToAsync(outputStream, ct);

			if (File.Exists(destinationPath)) {
				var backupPath = $"{destinationPath}.archive.bkup";
				Log.Information("Backing up {chunk} to {chunkBackup}", Path.GetFileName(destinationPath), Path.GetFileName(backupPath));
				File.Move(destinationPath, backupPath, overwrite: true);
			}

			File.Move(tempPath, destinationPath);

			return true;
		} catch (ChunkDeletedException) {
			Log.Warning("Failed to fetch {chunk} from the archive as it was deleted. This can happen if the archive is being scavenged.", chunkFile);
			return false;
		} catch (Exception ex) {
			Log.Error(ex, "Failed to fetch {chunk} from the archive. Retrying in {interval}", chunkFile, RetryInterval);
			await Task.Delay(RetryInterval, ct);
			return false;
		}
	}

	private async Task CommitChunk(string chunkPath) {
		await using var headerStream = File.OpenRead(chunkPath);
		var header = ChunkHeader.FromStream(headerStream);

		_writerCheckpoint.Write(header.ChunkEndPosition);
		_writerCheckpoint.Flush();
		Log.Debug("Moved {checkpoint} checkpoint forward to: 0x{position:X}", _writerCheckpoint.Name, header.ChunkEndPosition);

		_replicationCheckpoint.Write(header.ChunkEndPosition);
		_replicationCheckpoint.Flush();
		Log.Debug("Moved {checkpoint} checkpoint forward to: 0x{position:X}", _replicationCheckpoint.Name, header.ChunkEndPosition);
	}
}
