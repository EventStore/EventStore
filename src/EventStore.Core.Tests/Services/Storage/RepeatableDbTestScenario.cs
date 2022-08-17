using EventStore.Common.Utils;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogAbstraction;
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
using System;
using System.Threading.Tasks;
using EventStore.Core.Caching;

namespace EventStore.Core.Tests.Services.Storage {
	[TestFixture]
	public abstract class RepeatableDbTestScenario<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		protected readonly int MaxEntriesInMemTable;
		protected TableIndex<TStreamId> TableIndex;
		protected IReadIndex<TStreamId> ReadIndex;
		protected LogFormatAbstractor<TStreamId> _logFormat;

		protected DbResult DbRes;
		protected TFChunkDbCreationHelper<TLogFormat, TStreamId> DbCreationHelper;

		private readonly int _metastreamMaxCount;

		protected RepeatableDbTestScenario(int maxEntriesInMemTable = 20, int metastreamMaxCount = 1) {
			Ensure.Positive(maxEntriesInMemTable, "maxEntriesInMemTable");
			MaxEntriesInMemTable = maxEntriesInMemTable;
			_metastreamMaxCount = metastreamMaxCount;
		}

		public void CreateDb(params Rec[] records) {
			if (DbRes != null) {
				DbRes.Db.Close();
			}

			var indexDirectory = GetFilePathFor("index");
			_logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormatFactory.Create(new() {
				IndexDirectory = indexDirectory,
			});

			var dbConfig = TFChunkHelper.CreateSizedDbConfig(PathName, 0, chunkSize: 1024 * 1024);
			var dbHelper = new TFChunkDbCreationHelper<TLogFormat, TStreamId>(dbConfig, _logFormat);

			DbRes = dbHelper.Chunk(records).CreateDb();

			DbRes.Db.Config.WriterCheckpoint.Flush();
			DbRes.Db.Config.ChaserCheckpoint.Write(DbRes.Db.Config.WriterCheckpoint.Read());
			DbRes.Db.Config.ChaserCheckpoint.Flush();

			var readers = new ObjectPool<ITransactionFileReader>(
				"Readers", 2, 2, () => new TFChunkReader(DbRes.Db, DbRes.Db.Config.WriterCheckpoint));

			var lowHasher = _logFormat.LowHasher;
			var highHasher = _logFormat.HighHasher;
			var emptyStreamId = _logFormat.EmptyStreamId;
			TableIndex = new TableIndex<TStreamId>(indexDirectory, lowHasher, highHasher, emptyStreamId,
				() => new HashListMemTable(PTableVersions.IndexV3, MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				PTableVersions.IndexV3,
				int.MaxValue,
				Constants.PTableMaxReaderCountDefault,
				MaxEntriesInMemTable);
			_logFormat.StreamNamesProvider.SetTableIndex(TableIndex);

			var readIndex = new ReadIndex<TStreamId>(new NoopPublisher(),
				readers,
				TableIndex,
				_logFormat.StreamNameIndexConfirmer,
				_logFormat.StreamIds,
				_logFormat.StreamNamesProvider,
				_logFormat.EmptyStreamId,
				_logFormat.StreamIdValidator,
				_logFormat.StreamIdSizer,
				_logFormat.StreamExistenceFilter,
				_logFormat.StreamExistenceFilterReader,
				_logFormat.EventTypeIndexConfirmer,
				streamInfoCacheSettings: CacheSettings.Static("StreamInfo", 0),
				additionalCommitChecks: true,
				metastreamMaxCount: _metastreamMaxCount,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: DbRes.Db.Config.ReplicationCheckpoint,
				indexCheckpoint: DbRes.Db.Config.IndexCheckpoint);

			readIndex.IndexCommitter.Init(DbRes.Db.Config.ChaserCheckpoint.Read());
			ReadIndex = readIndex;
		}

		public override Task TestFixtureTearDown() {
			_logFormat?.Dispose();
			DbRes.Db.Close();
			return base.TestFixtureTearDown();
		}
	}
}
