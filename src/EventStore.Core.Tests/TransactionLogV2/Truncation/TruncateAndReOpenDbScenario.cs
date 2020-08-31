using System.IO;
using System.Threading.Tasks;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLogV2;
using EventStore.Core.TransactionLogV2.Checkpoint;
using EventStore.Core.TransactionLogV2.Chunks;
using EventStore.Core.TransactionLogV2.DataStructures;
using EventStore.Core.TransactionLogV2.FileNamingStrategy;
using EventStore.Core.TransactionLogV2.Hashes;
using EventStore.Core.TransactionLogV2.TestHelpers;
using EventStore.Core.Util;

namespace EventStore.Core.Tests.TransactionLogV2.Truncation {
	public abstract class TruncateAndReOpenDbScenario : TruncateScenario {
		protected TruncateAndReOpenDbScenario(int maxEntriesInMemTable = 100, int metastreamMaxCount = 1)
			: base(maxEntriesInMemTable, metastreamMaxCount) {
		}

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			ReOpenDb();
		}

		private void ReOpenDb() {
			Db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, WriterCheckpoint, ChaserCheckpoint));

			Db.Open();

			var readers = new ObjectPool<ITransactionFileReader>("Readers", 2, 5,
				() => new TFChunkReader(Db, Db.Config.WriterCheckpoint));
			var lowHasher = new XXHashUnsafe();
			var highHasher = new Murmur3AUnsafe();
			TableIndex = new TableIndex(Path.Combine(PathName, "index"), lowHasher, highHasher,
				() => new HashListMemTable(PTableVersions.IndexV3, MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				PTableVersions.IndexV3,
				int.MaxValue,
				Constants.PTableMaxReaderCountDefault,
				MaxEntriesInMemTable);
			ReadIndex = new ReadIndex(new NoopPublisher(),
				readers,
				TableIndex,
				0,
				additionalCommitChecks: true,
				metastreamMaxCount: MetastreamMaxCount,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: Db.Config.ReplicationCheckpoint,
				indexCheckpoint: Db.Config.IndexCheckpoint);
			((ReadIndex)ReadIndex).IndexCommitter.Init(ChaserCheckpoint.Read());
		}
	}
}
