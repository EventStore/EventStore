// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading.Tasks;
using EventStore.Core.Tests;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.XUnit.Tests.Scavenge.Infrastructure;
using EventStore.Core.XUnit.Tests.Scavenge.Sqlite;
using Xunit;
using static EventStore.Core.XUnit.Tests.Scavenge.Infrastructure.StreamMetadatas;

namespace EventStore.Core.XUnit.Tests.Scavenge;

// for testing the maxage functionality specifically
public class MaxAgeTests : SqliteDbPerTest<MaxAgeTests> {
	[Fact]
	public async Task simple_maxage() {
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithDbPath(Fixture.Directory)
			.WithDb(x => x
				.Chunk(
					Rec.Write(t++, "ab-1", timestamp: Expired),
					Rec.Write(t++, "ab-1", timestamp: Expired),
					Rec.Write(t++, "ab-1", timestamp: Active),
					Rec.Write(t++, "ab-1", timestamp: Active),
					Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxAgeMetadata))
				.Chunk(ScavengePointRec(t++)))
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.RunAsync(
				x => new[] {
					x.Recs[0].KeepIndexes(2, 3, 4),
					x.Recs[1],
				},
				Scenario.CollideEverything
					// index is the same as the log because it tries to find the log records
					// and fails, so must remove the index entries.
					? default(Func<DbResult, LogRecord[][]>)
					// records kept in the index because they are 'maybe' expired
					: x => new[] {
						x.Recs[0],
						x.Recs[1],
					});
	}

	[Fact]
	public async Task keep_last_event() {
		// records kept in the index because they are 'maybe' expired
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithDbPath(Fixture.Directory)
			.WithDb(x => x
				.Chunk(
					Rec.Write(t++, "ab-1", timestamp: Expired),
					Rec.Write(t++, "ab-1", timestamp: Expired),
					Rec.Write(t++, "ab-1", timestamp: Expired),
					Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxAgeMetadata))
				.Chunk(ScavengePointRec(t++)))
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.RunAsync(
				x => new[] {
					x.Recs[0].KeepIndexes(2, 3),
					x.Recs[1],
				},
				Scenario.CollideEverything
					? default(Func<DbResult, LogRecord[][]>)
					: x => new[] {
						x.Recs[0],
						x.Recs[1],
					});
	}

	[Fact]
	public async Task whole_chunk_expired() {
		// the records can be removed from the chunks and the index
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithDbPath(Fixture.Directory)
			.WithDb(x => x
				.Chunk(
					Rec.Write(t++, "ab-1", timestamp: Expired),
					Rec.Write(t++, "ab-1", timestamp: Expired),
					Rec.Write(t++, "ab-1", timestamp: Expired))
				.Chunk(
					Rec.Write(t++, "ab-1", timestamp: Active),
					Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxAgeMetadata))
				.Chunk(ScavengePointRec(t++)))
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.RunAsync(
				x => new[] {
					x.Recs[0].KeepIndexes(),
					x.Recs[1].KeepIndexes(0, 1),
					x.Recs[2],
				});
	}

	[Fact]
	public async Task whole_chunk_expired_keep_last_event() {
		// the records can be removed from the chunks and the index
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithDbPath(Fixture.Directory)
			.WithDb(x => x
				.Chunk(
					Rec.Write(t++, "ab-1", timestamp: Expired),
					Rec.Write(t++, "ab-1", timestamp: Expired),
					Rec.Write(t++, "ab-1", timestamp: Expired))
				.Chunk(
					Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxAgeMetadata))
				.Chunk(ScavengePointRec(t++)))
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.RunAsync(
				x => new[] {
					x.Recs[0].KeepIndexes(2),
					x.Recs[1].KeepIndexes(0),
					x.Recs[2],
				});
	}

	[Fact]
	public async Task whole_chunk_active() {
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithDbPath(Fixture.Directory)
			.WithDb(x => x
				.Chunk(
					Rec.Write(t++, "ab-1", timestamp: Active),
					Rec.Write(t++, "ab-1", timestamp: Active),
					Rec.Write(t++, "ab-1", timestamp: Active))
				.Chunk(
					Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxAgeMetadata))
				.Chunk(ScavengePointRec(t++)))
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.RunAsync(
				x => new[] {
					x.Recs[0],
					x.Recs[1],
					x.Recs[2],
				});
	}
}
