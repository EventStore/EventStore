﻿using System.Threading.Tasks;
using EventStore.Core.Tests;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.XUnit.Tests.Scavenge.Sqlite;
using Xunit;
using static EventStore.Core.XUnit.Tests.Scavenge.StreamMetadatas;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	// for testing the truncatebefore functionality specifically
	public class TruncateBeforeTests : SqliteDbPerTest<TruncateBeforeTests> {
		[Fact]
		public async Task simple_truncatebefore() {
			var t = 0;
			await new Scenario<LogFormat.V2, string>()
				.WithDbPath(Fixture.Directory)
				.WithDb(x => x
					.Chunk(
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "$$ab-1", "$metadata", metadata: TruncateBefore3))
					.Chunk(ScavengePointRec(t++)))
				.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
				.RunAsync(x => new[] {
					x.Recs[0].KeepIndexes(3, 4),
					x.Recs[1],
				});
		}

		[Fact]
		public async Task keep_last_event() {
			var t = 0;
			await new Scenario<LogFormat.V2, string>()
				.WithDbPath(Fixture.Directory)
				.WithDb(x => x
					.Chunk(
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "$$ab-1", "$metadata", metadata: TruncateBefore4))
					.Chunk(ScavengePointRec(t++)))
				.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
				.RunAsync(x => new[] {
					x.Recs[0].KeepIndexes(1,2),
					x.Recs[1],
				});
		}
	}
}
