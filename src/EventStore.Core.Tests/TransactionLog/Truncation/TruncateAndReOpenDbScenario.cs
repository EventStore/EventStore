using System.IO;
using System.Threading.Tasks;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Util;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Tests.Services;

namespace EventStore.Core.Tests.TransactionLog.Truncation {
	public abstract class TruncateAndReOpenDbScenario<TLogFormat, TStreamId> : TruncateScenario<TLogFormat, TStreamId> {
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
			var lowHasher = _logFormat.LowHasher;
			var highHasher = _logFormat.HighHasher;
			var emptyStreamId = _logFormat.EmptyStreamId;
			TableIndex = new TableIndex<TStreamId>(Path.Combine(PathName, "index"), lowHasher, highHasher, emptyStreamId,
				() => new HashListMemTable(PTableVersions.IndexV3, MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				PTableVersions.IndexV3,
				int.MaxValue,
				Constants.PTableMaxReaderCountDefault,
				MaxEntriesInMemTable);
			var readIndex = new ReadIndex<TStreamId>(new NoopPublisher(),
				readers,
				TableIndex,
				_logFormat.StreamIds,
				_logFormat.StreamNamesProvider,
				_logFormat.EmptyStreamId,
				_logFormat.StreamIdValidator,
				_logFormat.StreamIdSizer,
				0,
				additionalCommitChecks: true,
				metastreamMaxCount: MetastreamMaxCount,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: Db.Config.ReplicationCheckpoint,
				indexCheckpoint: Db.Config.IndexCheckpoint);
			readIndex.IndexCommitter.Init(ChaserCheckpoint.Read());
			ReadIndex = new TestReadIndex<TStreamId>(readIndex, _logFormat.StreamNameIndex);
		}
	}
}
