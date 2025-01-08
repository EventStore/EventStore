// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.TransactionLog.Chunks;

// Uses a naming strategy (which can access the chunk storage) to enumerate through
// the chunks in the storage, in ascending order, determining whether each chunk file
// is the Latest for its (logical) chunkNumber, an older version of its chunkNumber,
// and spotting if any chunkNumbers are missing entirely.
public class TFChunkEnumerator {
	private string[] _allFiles;
	private readonly Dictionary<string, int> _nextChunkNumber;
	private readonly IChunkFileSystem _fileSystem;

	public TFChunkEnumerator(IChunkFileSystem fileSystem) {
		_allFiles = null;
		_nextChunkNumber = [];
		_fileSystem = fileSystem;
	}

	// lastChunkNumber is not a filter/limit, it is used to spot missing chunks
	// getNextChunkNumber is used from tests only. todo: do we really need it
	public async IAsyncEnumerable<TFChunkInfo> EnumerateChunks(
		int lastChunkNumber,
		Func<string, int, int, CancellationToken, ValueTask<int>> getNextChunkNumber = null,
		[EnumeratorCancellation] CancellationToken token = default) {

		getNextChunkNumber ??= GetNextChunkNumber;

		if (_allFiles is null) {
			var allFiles = _fileSystem.NamingStrategy.GetAllPresentFiles();
			Array.Sort(allFiles, StringComparer.CurrentCultureIgnoreCase);
			_allFiles = allFiles;
		}

		int expectedChunkNumber = 0;
		for (int i = 0; i < _allFiles.Length; i++) {
			var chunkFileName = _allFiles[i];
			var chunkNumber = _fileSystem.NamingStrategy.GetIndexFor(Path.GetFileName(_allFiles[i]));
			var nextChunkNumber = -1;
			if (i + 1 < _allFiles.Length)
				nextChunkNumber = _fileSystem.NamingStrategy.GetIndexFor(Path.GetFileName(_allFiles[i+1]));

			if (chunkNumber < expectedChunkNumber) { // present in an earlier, merged, chunk
				yield return new OldVersion(chunkFileName, chunkNumber);
				continue;
			}

			if (chunkNumber > expectedChunkNumber) { // one or more chunks are missing
				for (int j = expectedChunkNumber; j < chunkNumber; j++) {
					yield return new MissingVersion(_fileSystem.NamingStrategy.GetFilenameFor(j, 0), j);
				}
				// set the expected chunk number to prevent calling onFileMissing() again for the same chunk numbers
				expectedChunkNumber = chunkNumber;
			}

			if (chunkNumber == nextChunkNumber) { // there is a newer version of this chunk
				yield return new OldVersion(chunkFileName, chunkNumber);
			} else { // latest version of chunk with the expected chunk number
				expectedChunkNumber = await getNextChunkNumber(chunkFileName, chunkNumber, _fileSystem.NamingStrategy.GetVersionFor(Path.GetFileName(chunkFileName)), token);
				yield return new LatestVersion(chunkFileName, chunkNumber, expectedChunkNumber - 1);
			}
		}

		for (int i = expectedChunkNumber; i <= lastChunkNumber; i++) {
			yield return new MissingVersion(_fileSystem.NamingStrategy.GetFilenameFor(i, 0), i);
		}
	}

	private async ValueTask<int> GetNextChunkNumber(string chunkFileName, int chunkNumber, int chunkVersion, CancellationToken token) {
		if (chunkVersion is 0)
			return chunkNumber + 1;

		// we only cache next chunk numbers for chunks having a non-zero version
		if (_nextChunkNumber.TryGetValue(chunkFileName, out var nextChunkNumber))
			return nextChunkNumber;

		var header = await _fileSystem.ReadHeaderAsync(chunkFileName, token);
		_nextChunkNumber[chunkFileName] = header.ChunkEndNumber + 1;
		return header.ChunkEndNumber + 1;
	}
}
