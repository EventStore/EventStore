// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Buffers;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.TransactionLog.Chunks.TFChunk;

public sealed class ChunkLocalFileSystem : IChunkFileSystem {
	private readonly VersionedPatternFileNamingStrategy _strategy;

	public ChunkLocalFileSystem(VersionedPatternFileNamingStrategy namingStrategy) {
		_strategy = namingStrategy;
	}

	public ChunkLocalFileSystem(string path, string chunkFilePrefix = "chunk-")
		: this(new(path, chunkFilePrefix)) {
	}

	public IVersionedFileNamingStrategy NamingStrategy => _strategy;

	// is used from tests only
	public Func<string, int, int, CancellationToken, ValueTask<int>> ChunkNumberProvider {
		get;
		init;
	}

	public ValueTask<IChunkHandle> OpenForReadAsync(string fileName, IChunkFileSystem.ReadOptimizationHint hint, CancellationToken token) {
		ValueTask<IChunkHandle> task;
		try {
			var options = new FileStreamOptions {
				Mode = FileMode.Open,
				Access = FileAccess.Read,
				Share = FileShare.ReadWrite,
				Options = ChunkFileHandle.ConvertToFileOptions(hint),
			};

			task = new(new ChunkFileHandle(fileName, options));
		} catch (FileNotFoundException) {
			task = ValueTask.FromException<IChunkHandle>(
				new CorruptDatabaseException(new ChunkNotFoundException(fileName)));
		} catch (Exception e) {
			task = ValueTask.FromException<IChunkHandle>(e);
		}

		return task;
	}

	public async ValueTask<ChunkHeader> ReadHeaderAsync(string fileName, CancellationToken token) {
		using var handle = File.OpenHandle(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
			FileOptions.Asynchronous);

		var length = RandomAccess.GetLength(handle);
		if (length < ChunkFooter.Size + ChunkHeader.Size) {
			throw new CorruptDatabaseException(new BadChunkInDatabaseException(
				$"Chunk file '{fileName}' is bad. It does not have enough size for header and footer. File size is {length} bytes."));
		}

		using var buffer = Memory.AllocateExactly<byte>(ChunkHeader.Size);
		await RandomAccess.ReadAsync(handle, buffer.Memory, 0L, token);
		return new(buffer.Span);
	}

	public async ValueTask<ChunkFooter> ReadFooterAsync(string fileName, CancellationToken token) {
		using var handle = File.OpenHandle(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
			FileOptions.Asynchronous);

		var length = RandomAccess.GetLength(handle);
		if (length < ChunkFooter.Size + ChunkHeader.Size) {
			throw new CorruptDatabaseException(new BadChunkInDatabaseException(
				$"Chunk file '{fileName}' is bad. It does not have enough size for header and footer. File size is {length} bytes."));
		}

		using var buffer = Memory.AllocateExactly<byte>(ChunkFooter.Size);
		await RandomAccess.ReadAsync(handle, buffer.Memory, length - ChunkFooter.Size, token);
		return new(buffer.Span);
	}

	public IChunkFileSystem.IChunkEnumerable GetChunks()
		=> new TFChunkEnumerable(this) { ChunkNumberProvider = ChunkNumberProvider };

	private sealed class TFChunkEnumerable(ChunkLocalFileSystem fileSystem)
		: Dictionary<string, int>, IChunkFileSystem.IChunkEnumerable {
		private string[] _allFiles;
		private readonly Func<string, int, int, CancellationToken, ValueTask<int>> _chunkNumberProvider;

		public required Func<string, int, int, CancellationToken, ValueTask<int>> ChunkNumberProvider {
			init => _chunkNumberProvider = value ?? GetNextChunkNumber;
		}

		public int LastChunkNumber { get; set; }

		public async IAsyncEnumerator<TFChunkInfo> GetAsyncEnumerator(CancellationToken token = default) {
			if (_allFiles is null) {
				var allFiles = fileSystem._strategy.GetAllPresentFiles();
				Array.Sort(allFiles, StringComparer.CurrentCultureIgnoreCase);
				_allFiles = allFiles;
			}

			int expectedChunkNumber = 0;
			for (int i = 0; i < _allFiles.Length; i++) {
				var chunkFileName = _allFiles[i];
				var chunkNumber = fileSystem._strategy.GetIndexFor(Path.GetFileName(_allFiles[i]));
				var nextChunkNumber = -1;
				if (i + 1 < _allFiles.Length)
					nextChunkNumber = fileSystem._strategy.GetIndexFor(Path.GetFileName(_allFiles[i + 1]));

				if (chunkNumber < expectedChunkNumber) {
					// present in an earlier, merged, chunk
					yield return new OldVersion(chunkFileName, chunkNumber);
					continue;
				}

				if (chunkNumber > expectedChunkNumber) {
					// one or more chunks are missing
					for (int j = expectedChunkNumber; j < chunkNumber; j++) {
						yield return new MissingVersion(fileSystem._strategy.GetFilenameFor(j, 0), j);
					}

					// set the expected chunk number to prevent calling onFileMissing() again for the same chunk numbers
					expectedChunkNumber = chunkNumber;
				}

				if (chunkNumber == nextChunkNumber) {
					// there is a newer version of this chunk
					yield return new OldVersion(chunkFileName, chunkNumber);
				} else {
					// latest version of chunk with the expected chunk number
					expectedChunkNumber = await _chunkNumberProvider(chunkFileName, chunkNumber,
						fileSystem._strategy.GetVersionFor(Path.GetFileName(chunkFileName)), token);
					yield return new LatestVersion(chunkFileName, chunkNumber, expectedChunkNumber - 1);
				}
			}

			for (int i = expectedChunkNumber; i <= LastChunkNumber; i++) {
				yield return new MissingVersion(fileSystem._strategy.GetFilenameFor(i, 0), i);
			}
		}

		private async ValueTask<int> GetNextChunkNumber(string chunkFileName, int chunkNumber, int chunkVersion, CancellationToken token) {
			if (chunkVersion is 0)
				return chunkNumber + 1;

			// we only cache next chunk numbers for chunks having a non-zero version
			if (TryGetValue(chunkFileName, out var nextChunkNumber))
				return nextChunkNumber;

			var header = await fileSystem.ReadHeaderAsync(chunkFileName, token);
			this[chunkFileName] = header.ChunkEndNumber + 1;
			return header.ChunkEndNumber + 1;
		}
	}
}
