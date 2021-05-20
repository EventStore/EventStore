using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using EventStore.Core.Util;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers {
	[TestFixture]
	public abstract class ScavengeTestScenario<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		protected ITestReadIndex<TStreamId> ReadIndex;

		protected TFChunkDb Db {
			get { return _dbResult.Db; }
		}

		private readonly int _metastreamMaxCount;
		private DbResult _dbResult;
		private ILogRecord[][] _keptRecords;
		private bool _checked;

		protected virtual bool UnsafeIgnoreHardDelete() {
			return false;
		}

		protected ScavengeTestScenario(int metastreamMaxCount = 1) {
			_metastreamMaxCount = metastreamMaxCount;
		}

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			var dbConfig = TFChunkHelper.CreateSizedDbConfig(PathName, 0, chunkSize: 1024 * 1024);
			var dbCreationHelper = new TFChunkDbCreationHelper<TLogFormat, TStreamId>(dbConfig);
			_dbResult = CreateDb(dbCreationHelper);
			_keptRecords = KeptRecords(_dbResult);

			_dbResult.Db.Config.WriterCheckpoint.Flush();
			_dbResult.Db.Config.ChaserCheckpoint.Write(_dbResult.Db.Config.WriterCheckpoint.Read());
			_dbResult.Db.Config.ChaserCheckpoint.Flush();

			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
			var indexPath = Path.Combine(PathName, "index");
			var readerPool = new ObjectPool<ITransactionFileReader>(
				"ReadIndex readers pool", Constants.PTableInitialReaderCount, Constants.PTableMaxReaderCountDefault,
				() => new TFChunkReader(_dbResult.Db, _dbResult.Db.Config.WriterCheckpoint));
			var lowHasher = logFormat.LowHasher;
			var highHasher = logFormat.HighHasher;
			var emptyStreamId = logFormat.EmptyStreamId;
			var tableIndex = new TableIndex<TStreamId>(indexPath, lowHasher, highHasher, emptyStreamId,
				() => new HashListMemTable(PTableVersions.IndexV3, maxSize: 200),
				() => new TFReaderLease(readerPool),
				PTableVersions.IndexV3,
				5, Constants.PTableMaxReaderCountDefault,
				maxSizeForMemory: 100,
				maxTablesPerLevel: 2);
			var readIndex = new ReadIndex<TStreamId>(new NoopPublisher(), readerPool, tableIndex,
				logFormat.StreamIds,
				logFormat.StreamNamesProvider,
				logFormat.EmptyStreamId,
				logFormat.StreamIdValidator,
				logFormat.StreamIdSizer,
				100, true, _metastreamMaxCount,
				Opts.HashCollisionReadLimitDefault, Opts.SkipIndexScanOnReadsDefault,
				_dbResult.Db.Config.ReplicationCheckpoint,_dbResult.Db.Config.IndexCheckpoint);
			readIndex.IndexCommitter.Init(_dbResult.Db.Config.WriterCheckpoint.Read());
			ReadIndex = new TestReadIndex<TStreamId>(readIndex, logFormat.StreamNameIndex);

			var scavenger = new TFChunkScavenger<TStreamId>(_dbResult.Db, new FakeTFScavengerLog(), tableIndex, ReadIndex,
				logFormat.SystemStreams,
				unsafeIgnoreHardDeletes: UnsafeIgnoreHardDelete());
			await scavenger.Scavenge(alwaysKeepScavenged: true, mergeChunks: false);
		}

		public override async Task TestFixtureTearDown() {
			ReadIndex.Close();
			_dbResult.Db.Close();

			await base.TestFixtureTearDown();

			if (!_checked)
				throw new Exception("Records were not checked. Probably you forgot to call CheckRecords() method.");
		}

		protected abstract DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator);

		protected abstract ILogRecord[][] KeptRecords(DbResult dbResult);

		protected void CheckRecords() {
			_checked = true;
			Assert.AreEqual(_keptRecords.Length, _dbResult.Db.Manager.ChunksCount, "Wrong chunks count.");

			for (int i = 0; i < _keptRecords.Length; ++i) {
				var chunk = _dbResult.Db.Manager.GetChunk(i);

				var chunkRecords = new List<ILogRecord>();
				RecordReadResult result = chunk.TryReadFirst();
				while (result.Success) {
					chunkRecords.Add(result.LogRecord);
					result = chunk.TryReadClosestForward((int)result.NextPosition);
				}

				Assert.AreEqual(_keptRecords[i].Length, chunkRecords.Count, "Wrong number of records in chunk #{0}", i);

				for (int j = 0; j < _keptRecords[i].Length; ++j) {
					Assert.AreEqual(_keptRecords[i][j], chunkRecords[j], "Wrong log record #{0} read from chunk #{1}",
						j, i);
				}
			}
		}
	}
}
