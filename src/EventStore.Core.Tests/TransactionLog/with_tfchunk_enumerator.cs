// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog;

[TestFixture]
public class with_tfchunk_enumerator : SpecificationWithDirectory {

	[Test]
	public async Task iterates_chunks_with_correct_callback_order() {
		File.Create(GetFilePathFor("foo")).Close(); // should be ignored
		File.Create(GetFilePathFor("bla")).Close(); // should be ignored
		File.Create(GetFilePathFor("chunk-000001.000000.tmp")).Close(); // should be ignored
		File.Create(GetFilePathFor("chunk-001.000")).Close(); // should be ignored

		// chunk 0 is missing
		File.Create(GetFilePathFor("chunk-000001.000000")).Close(); // chunks 1 - 1 (latest)
		File.Create(GetFilePathFor("chunk-000002.000001")).Close(); // chunks 2 - 2 (latest)
		// chunks 3 & 4 are missing
		File.Create(GetFilePathFor("chunk-000005.000000")).Close(); // chunks 5 - 5 (old)
		File.Create(GetFilePathFor("chunk-000005.000001")).Close(); // chunks 5 - 6 (old)
		File.Create(GetFilePathFor("chunk-000005.000002")).Close(); // chunks 5 - 7 (latest)
		File.Create(GetFilePathFor("chunk-000006.000000")).Close(); // chunks 6 - 6 (old)
		// chunk 7 is not missing - it's merged with chunk 5
		File.Create(GetFilePathFor("chunk-000008.000007")).Close(); // chunks 8 - 8 (latest)
		// chunk 9 is missing
		File.Create(GetFilePathFor("chunk-000010.000005")).Close(); // chunks 10 - 14 (latest)
		// chunks 15 & 16 are missing

		var strategy = new VersionedPatternFileNamingStrategy(PathName, "chunk-");
		var chunkEnumerator = new TFChunkEnumerator(strategy, ChunkLocalFileSystem.Instance);
		var result = new List<string>();
		ValueTask<int> GetNextFileNumber(string chunk, int chunkNumber, int chunkVersion, CancellationToken token) {
			return Path.GetFileName(chunk) switch {
				"chunk-000001.000000" => new(2),
				"chunk-000002.000001" => new(3),
				"chunk-000005.000000" => new(6),
				"chunk-000005.000001" => new(7),
				"chunk-000005.000002" => new(8),
				"chunk-000006.000000" => new(7),
				"chunk-000008.000007" => new(9),
				"chunk-000010.000005" => new(15),
				_ => ValueTask.FromException<int>(new Exception($"Unexpected file: {chunk}"))
			};
		}

		await foreach (var chunkInfo in chunkEnumerator.EnumerateChunks(16, GetNextFileNumber)) {
			switch (chunkInfo) {
				case LatestVersion(var fileName, var start, var end):
					result.Add($"latest {Path.GetFileName(fileName)} {start}-{end}");
					break;
				case OldVersion(var fileName, var start):
					result.Add($"old {Path.GetFileName(fileName)} {start}");
					break;
				case MissingVersion(var fileName, var start):
					result.Add($"missing {Path.GetFileName(fileName)} {start}");
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(chunkInfo));
			}
		}

		var expectedResult = new List<string> {
			"missing chunk-000000.000000 0",
			"latest chunk-000001.000000 1-1",
			"latest chunk-000002.000001 2-2",
			"missing chunk-000003.000000 3",
			"missing chunk-000004.000000 4",
			"old chunk-000005.000000 5",
			"old chunk-000005.000001 5",
			"latest chunk-000005.000002 5-7",
			"old chunk-000006.000000 6",
			"latest chunk-000008.000007 8-8",
			"missing chunk-000009.000000 9",
			"latest chunk-000010.000005 10-14",
			"missing chunk-000015.000000 15",
			"missing chunk-000016.000000 16"
		};
		Assert.AreEqual(expectedResult, result);
	}
}
