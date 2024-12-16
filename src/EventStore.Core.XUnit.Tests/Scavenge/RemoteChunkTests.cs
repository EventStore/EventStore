// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;
using EventStore.Core.Tests;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.XUnit.Tests.Scavenge.Sqlite;
using Xunit;
using static EventStore.Core.XUnit.Tests.Scavenge.StreamMetadatas;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class RemoteChunkTests : SqliteDbPerTest<RemoteChunkTests> {
	[Fact]
	public async Task remote_chunks_are_not_executed() {
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithDbPath(Fixture.Directory)
			.WithDb(x => x
				.RemoteChunk(
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"))
				.Chunk(
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxCount1))
				.Chunk(
					ScavengePointRec(t++)))
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.RunAsync(
				x => [
					x.Recs[0], // remote chunk still has its records
					x.Recs[1].KeepIndexes(1, 2),
					x.Recs[2],
				],
				x => [
					x.Recs[0].KeepNone(), // index entries for remote chunk are still removed
					x.Recs[1].KeepIndexes(1, 2),
					x.Recs[2],
				]);
	}
}
