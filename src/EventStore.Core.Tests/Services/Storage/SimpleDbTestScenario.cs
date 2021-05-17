using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage {
	[TestFixture]
	public abstract class SimpleDbTestScenario<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		protected readonly int MaxEntriesInMemTable;
		protected TableIndex<TStreamId> TableIndex;
		protected ITestReadIndex<TStreamId> ReadIndex;

		protected DbResult DbRes;

		protected abstract DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator);

		private readonly long _metastreamMaxCount;

		protected SimpleDbTestScenario(int maxEntriesInMemTable = 20, long metastreamMaxCount = 1) {
			Ensure.Positive(maxEntriesInMemTable, "maxEntriesInMemTable");
			MaxEntriesInMemTable = maxEntriesInMemTable;
			_metastreamMaxCount = metastreamMaxCount;
		}

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			var dbConfig = TFChunkHelper.CreateSizedDbConfig(PathName, 0, chunkSize: 1024 * 1024);
			var dbCreationHelper = new TFChunkDbCreationHelper<TLogFormat, TStreamId>(dbConfig);

			DbRes = CreateDb(dbCreationHelper);

			DbRes.Db.Config.WriterCheckpoint.Flush();
			DbRes.Db.Config.ChaserCheckpoint.Write(DbRes.Db.Config.WriterCheckpoint.Read());
			DbRes.Db.Config.ChaserCheckpoint.Flush();

			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
			var readers = new ObjectPool<ITransactionFileReader>(
				"Readers", 2, 2, () => new TFChunkReader(DbRes.Db, DbRes.Db.Config.WriterCheckpoint));

			var lowHasher = logFormat.LowHasher;
			var highHasher = logFormat.HighHasher;
			var emptyStreamId = logFormat.EmptyStreamId;
			TableIndex = new TableIndex<TStreamId>(GetFilePathFor("index"), lowHasher, highHasher, emptyStreamId,
				() => new HashListMemTable(PTableVersions.IndexV2, MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				PTableVersions.IndexV2,
				int.MaxValue,
				Constants.PTableMaxReaderCountDefault,
				MaxEntriesInMemTable);

			var readIndex = new ReadIndex<TStreamId>(new NoopPublisher(),
				readers,
				TableIndex,
				logFormat.StreamIds,
				logFormat.StreamNamesProvider,
				logFormat.EmptyStreamId,
				logFormat.StreamIdValidator,
				logFormat.StreamIdSizer,
				0,
				additionalCommitChecks: true,
				metastreamMaxCount: _metastreamMaxCount,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: DbRes.Db.Config.ReplicationCheckpoint,
				indexCheckpoint: DbRes.Db.Config.IndexCheckpoint);

			readIndex.IndexCommitter.Init(DbRes.Db.Config.ChaserCheckpoint.Read());
			ReadIndex = new TestReadIndex<TStreamId>(readIndex, logFormat.StreamNameIndex);
		}

		public override Task TestFixtureTearDown() {
			DbRes.Db.Close();

			return base.TestFixtureTearDown();
		}
	}
}
