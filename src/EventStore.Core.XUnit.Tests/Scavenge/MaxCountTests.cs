using System.Threading.Tasks;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.XUnit.Tests.Scavenge.Sqlite;
using Xunit;
using static EventStore.Core.XUnit.Tests.Scavenge.StreamMetadatas;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	// for testing the maxcount functionality specifically
	public class MaxCountTests : SqliteDbPerTest<MaxCountTests> {
		[Fact]
		public async Task simple_maxcount() {
			var t = 0;
			await new Scenario()
				.WithDbPath(Fixture.Directory)
				.WithDb(x => x
					.Chunk(
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxCount1))
					.Chunk(ScavengePointRec(t++)))
				.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
				.RunAsync(x => new[] {
					x.Recs[0].KeepIndexes(3, 4),
					x.Recs[1],
				});
		}

		[Fact]
		public async Task no_stream() {
			var t = 0;
			await new Scenario()
				.WithDbPath(Fixture.Directory)
				.WithDb(x => x
					.Chunk(
						Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxCount1))
					.Chunk(ScavengePointRec(t++)))
				.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
				.RunAsync(x => new[] {
					x.Recs[0],
					x.Recs[1],
				});
		}

		[Fact]
		public async Task large_maxcount() {
			var t = 0;
			await new Scenario()
				.WithDbPath(Fixture.Directory)
				.WithDb(x => x
					.Chunk(
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxCount50))
					.Chunk(ScavengePointRec(t++)))
				.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
				.RunAsync(x => new[] {
					x.Recs[0],
					x.Recs[1],
				});
		}
	}
}
