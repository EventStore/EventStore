// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.Core.Tests;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.XUnit.Tests.Scavenge.Sqlite;
using Xunit;
using static EventStore.Core.XUnit.Tests.Scavenge.StreamMetadatas;

namespace EventStore.Core.XUnit.Tests.Scavenge;

public class ThresholdTests : SqliteDbPerTest<ThresholdTests> {
	[Fact]
	public async Task negative_threshold_executes_all_chunks() {
		var threshold = -1;
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithDbPath(Fixture.Directory)
			.WithDb(x => x
				// chunk 0: weight 2
				.Chunk(
					Rec.Write(t++, "ab-1"))
				// chunk 1: weight 4
				.Chunk(
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"))
				// chunk 2: weight 0
				.Chunk(
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxCount1),
					ScavengePointRec(t++, threshold: threshold))
				.CompleteLastChunk())
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.AssertTrace(
				Tracer.Line("Accumulating from start to SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done None"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Reading Chunk 0"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done Chunk 0"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Reading Chunk 1"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done Chunk 1"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Reading Chunk 2"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done Chunk 2"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.Line("Calculating SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Calculating SP-0 done None"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        SetDiscardPoints(98, Active, Discard before 3, Discard before 3)"),
				Tracer.Line("        Checkpoint: Calculating SP-0 done Hash: 98"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.Line("Executing chunks for SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done None"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Retained Chunk 0-0"),
				Tracer.Line("    Opening Chunk 0-0"),
				Tracer.Line("    Switched in chunk-000000.000001"), // executed
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done Chunk 0"),
				Tracer.Line("    Commit"),

				Tracer.Line("    Retained Chunk 1-1"),
				Tracer.Line("    Opening Chunk 1-1"),
				Tracer.Line("    Switched in chunk-000001.000001"), // executed
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done Chunk 1"),
				Tracer.Line("    Commit"),

				Tracer.Line("    Retained Chunk 2-2"),
				Tracer.Line("    Opening Chunk 2-2"),
				Tracer.Line("    Switched in chunk-000002.000001"), // executed
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done Chunk 2"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.AnythingElse)
			.RunAsync(
				x => new[] {
					x.Recs[0].KeepIndexes(), // executed
					x.Recs[1].KeepIndexes(), // executed
					x.Recs[2], // executed
				},
				x => new[] {
					x.Recs[0].KeepIndexes(),
					x.Recs[1].KeepIndexes(),
					x.Recs[2],
				});
	}

	[Fact]
	public async Task zero_threshold_executes_all_chunks_with_positive_weight() {
		var threshold = 0;
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithDbPath(Fixture.Directory)
			.WithDb(x => x
				// chunk 0: weight 2
				.Chunk(
					Rec.Write(t++, "ab-1"))
				// chunk 1: weight 4
				.Chunk(
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"))
				// chunk 2: weight 0
				.Chunk(
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxCount1),
					ScavengePointRec(t++, threshold: threshold))
				.CompleteLastChunk())
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.AssertTrace(
				Tracer.Line("Accumulating from start to SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done None"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Reading Chunk 0"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done Chunk 0"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Reading Chunk 1"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done Chunk 1"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Reading Chunk 2"),
				Tracer.Line("        Checkpoint: Accumulating SP-0 done Chunk 2"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.Line("Calculating SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Calculating SP-0 done None"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Begin"),
				Tracer.Line("        SetDiscardPoints(98, Active, Discard before 3, Discard before 3)"),
				Tracer.Line("        Checkpoint: Calculating SP-0 done Hash: 98"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.Line("Executing chunks for SP-0"),
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done None"),
				Tracer.Line("    Commit"),
				Tracer.Line("    Retained Chunk 0-0"),
				Tracer.Line("    Opening Chunk 0-0"),
				Tracer.Line("    Switched in chunk-000000.000001"), // executed
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done Chunk 0"),
				Tracer.Line("    Commit"),

				Tracer.Line("    Retained Chunk 1-1"),
				Tracer.Line("    Opening Chunk 1-1"),
				Tracer.Line("    Switched in chunk-000001.000001"), // executed
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done Chunk 1"),
				Tracer.Line("    Commit"),

				Tracer.Line("    Retained Chunk 2-2"),
				//               no opening or switch, not executed.
				Tracer.Line("    Begin"),
				Tracer.Line("        Checkpoint: Executing chunks for SP-0 done Chunk 2"),
				Tracer.Line("    Commit"),
				Tracer.Line("Done"),

				Tracer.AnythingElse)
			.RunAsync(
				x => new[] {
					x.Recs[0].KeepIndexes(), // executed
					x.Recs[1].KeepIndexes(), // executed
					x.Recs[2], // not executed
				},
				x => new[] {
					x.Recs[0].KeepIndexes(),
					x.Recs[1].KeepIndexes(),
					x.Recs[2],
				});
	}

	[Fact]
	public async Task positive_threshold_executes_all_chunks_that_exceed_it() {
		var threshold = 2;
		var t = 0;
		await new Scenario<LogFormat.V2, string>()
			.WithDbPath(Fixture.Directory)
			.WithDb(x => x
				// chunk 0: weight 2
				.Chunk(
					Rec.Write(t++, "ab-1"))
				// chunk 1: weight 4
				.Chunk(
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "ab-1"))
				// chunk 2: weight 0
				.Chunk(
					Rec.Write(t++, "ab-1"),
					Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxCount1),
					ScavengePointRec(t++, threshold: threshold))
				.CompleteLastChunk())
			.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
			.RunAsync(
				x => new[] {
					x.Recs[0], // not executed so still has its records
					x.Recs[1].KeepIndexes(), // executed
					x.Recs[2], // not executed
				},
				x => new[] {
					x.Recs[0].KeepIndexes(),
					x.Recs[1].KeepIndexes(),
					x.Recs[2],
				});
	}
}
