using System.Threading.Tasks;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.XUnit.Tests.Scavenge.Sqlite;
using Xunit;
using static EventStore.Core.XUnit.Tests.Scavenge.StreamMetadatas;

namespace EventStore.Core.XUnit.Tests.Scavenge {
	public class CombinationCriteriaTests : SqliteDbPerTest<CombinationCriteriaTests> {
		//qq testing: need more of these, check that the criteria work well in different combinations
		// and set in different orders
		[Fact]
		public async Task maxcount_then_tombstone() {
			var t = 0;
			await new Scenario()
				.WithDbPath(Fixture.Directory)
				.WithDb(x => x
					.Chunk(
						Rec.Write(t++, "$$ab-1", "$metadata", metadata: MaxCount2),
						Rec.Write(t++, "ab-1"),
						Rec.Write(t++, "ab-1"),
						Rec.CommittedDelete(t++, "ab-1"))
					.Chunk(ScavengePointRec(t++)))
				.WithState(x => x.WithConnectionPool(Fixture.DbConnectionPool))
				.RunAsync(x => new[] {
					x.Recs[0].KeepIndexes(3),
					x.Recs[1],
				});
		}
	}
}
