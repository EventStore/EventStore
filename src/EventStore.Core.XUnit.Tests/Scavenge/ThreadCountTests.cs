// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.Core.Tests;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.XUnit.Tests.Scavenge.Sqlite;
using Xunit;
using static EventStore.Core.XUnit.Tests.Scavenge.StreamMetadatas;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class ThreadCountTests : SqliteDbPerTest<ThreadCountTests> {
	[Theory]
	[InlineData(-1)]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(TFChunkScavenger.MaxThreadCount)]
	[InlineData(TFChunkScavenger.MaxThreadCount + 1)]
	public async Task runs_when_thread_count_too_high_or_low(int threads) {
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithThreads(threads)
			.WithDbPath(Fixture.Directory)
			.WithDb(x => x
				// lots of chunks so there is possibility of parallel execution of them
				.Chunk(
					Rec.Write(t++, "ab-1"))
				.Chunk(
					Rec.Write(t++, "ab-1"))
				.Chunk(
					Rec.Write(t++, "ab-1"))
				.Chunk(
					Rec.Write(t++, "ab-1"))
				.Chunk(
					Rec.Write(t++, "ab-1"))
				.Chunk(
					Rec.Write(t++, "ab-1"))
				.Chunk(
					Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxCount1))
				.Chunk(
					ScavengePointRec(t++)))
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.RunAsync(x => [
				x.Recs[0].KeepNone(),
				x.Recs[1].KeepNone(),
				x.Recs[2].KeepNone(),
				x.Recs[3].KeepNone(),
				x.Recs[4].KeepNone(),
				x.Recs[5].KeepIndexes(0),
				x.Recs[6].KeepIndexes(0),
				x.Recs[7].KeepIndexes(0),
			]);
	}
}