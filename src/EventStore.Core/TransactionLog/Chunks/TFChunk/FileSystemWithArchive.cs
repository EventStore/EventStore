// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Archive.Storage;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

// Adds archive storage to a filesystem.
public sealed class FileSystemWithArchive : IChunkFileSystem {
	private readonly int _chunkSize;
	private readonly ILocatorCodec _locatorCodec;
	private readonly IChunkFileSystem _localFileSystem;
	private readonly IArchiveStorageReader _archive;

	public FileSystemWithArchive(
		int chunkSize,
		ILocatorCodec locatorCodec,
		IChunkFileSystem localFileSystem,
		IArchiveStorageReader archive) {

		_chunkSize = chunkSize;
		_locatorCodec = locatorCodec;
		_localFileSystem = localFileSystem;
		_archive = archive;
	}

	public IVersionedFileNamingStrategy NamingStrategy =>
		_localFileSystem.NamingStrategy;

	public ValueTask<IChunkHandle> OpenForReadAsync(string fileName, bool reduceFileCachePressure, CancellationToken token) {
		var isRemote = _locatorCodec.Decode(fileName, out var decoded);
		if (!isRemote) {
			return _localFileSystem.OpenForReadAsync(decoded, reduceFileCachePressure, token);
		}

		throw new NotImplementedException();
	}

	public ValueTask<ChunkFooter> ReadFooterAsync(string fileName, CancellationToken token) {
		var isRemote = _locatorCodec.Decode(fileName, out var decoded);
		if (!isRemote) {
			return _localFileSystem.ReadFooterAsync(decoded, token);
		}

		throw new InvalidOperationException("Tried the read the footer of an archived chunk");
	}

	public ValueTask<ChunkHeader> ReadHeaderAsync(string fileName, CancellationToken token) {
		var isRemote = _locatorCodec.Decode(fileName, out var decoded);
		if (!isRemote) {
			return _localFileSystem.ReadHeaderAsync(decoded, token);
		}

		throw new InvalidOperationException("Tried the read the header of an archived chunk");
	}

	public IChunkFileSystem.IChunkEnumerable GetChunks() {
		return new ChunkEnumerableWithArchive(this);
	}

	sealed class ChunkEnumerableWithArchive(FileSystemWithArchive fileSystem) : IChunkFileSystem.IChunkEnumerable {
		private readonly IChunkFileSystem.IChunkEnumerable _localChunks = fileSystem.GetChunks();

		public int LastChunkNumber {
			get => _localChunks.LastChunkNumber;
			set => _localChunks.LastChunkNumber = value;
		}

		public async IAsyncEnumerator<TFChunkInfo> GetAsyncEnumerator(CancellationToken token = default) {
			var archiveCheckpoint = await fileSystem._archive.GetCheckpoint(token);
			var firstChunkNotInArchive = (int)(archiveCheckpoint / fileSystem._chunkSize);

			await foreach (var chunkInfo in _localChunks.WithCancellation(token)) {
				switch (chunkInfo) {
					// replace missing local versions with latest from archive if they
					// are present there
					case MissingVersion(_, var chunkNumber) when (chunkNumber < firstChunkNotInArchive): {
						yield return CreateLatestVersionInArchive(chunkNumber);
						break;
					}

					default:
						yield return chunkInfo;
						break;
				}
			}
		}

		LatestVersion CreateLatestVersionInArchive(int chunkNumber) {
			var archiveObjectName = fileSystem._archive.ChunkNamer.GetFileNameFor(chunkNumber);
			var fileName = fileSystem._locatorCodec.EncodeRemoteName(archiveObjectName);
			return new LatestVersion(fileName, chunkNumber, chunkNumber);
		}
	}
}
