using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Settings;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using EventStore.Core.Util;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers {
	[TestFixture]
	public abstract class ScavengeTestScenario : SpecificationWithDirectoryPerTestFixture {
		protected IReadIndex ReadIndex;

		protected TFChunkDb Db {
			get { return _dbResult.Db; }
		}

		private readonly int _metastreamMaxCount;
		private DbResult _dbResult;
		private LogRecord[][] _keptRecords;
		private bool _checked;

		protected virtual bool UnsafeIgnoreHardDelete() {
			return false;
		}

		protected ScavengeTestScenario(int metastreamMaxCount = 1) {
			_metastreamMaxCount = metastreamMaxCount;
		}

		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			var dbConfig = TFChunkHelper.CreateDbConfig(PathName, 0, chunkSize: 1024 * 1024);
			var dbCreationHelper = new TFChunkDbCreationHelper(dbConfig);
			_dbResult = CreateDb(dbCreationHelper);
			_keptRecords = KeptRecords(_dbResult);

			_dbResult.Db.Config.WriterCheckpoint.Flush();
			_dbResult.Db.Config.ChaserCheckpoint.Write(_dbResult.Db.Config.WriterCheckpoint.Read());
			_dbResult.Db.Config.ChaserCheckpoint.Flush();

			var indexPath = Path.Combine(PathName, "index");
			var readerPool = new ObjectPool<ITransactionFileReader>(
				"ReadIndex readers pool", ESConsts.PTableInitialReaderCount, ESConsts.PTableMaxReaderCount,
				() => new TFChunkReader(_dbResult.Db, _dbResult.Db.Config.WriterCheckpoint));
			var lowHasher = new XXHashUnsafe();
			var highHasher = new Murmur3AUnsafe();
			var tableIndex = new TableIndex(indexPath, lowHasher, highHasher,
				() => new HashListMemTable(PTableVersions.IndexV3, maxSize: 200),
				() => new TFReaderLease(readerPool),
				PTableVersions.IndexV3,
				5,
				maxSizeForMemory: 100,
				maxTablesPerLevel: 2);
			ReadIndex = new ReadIndex(new NoopPublisher(), readerPool, tableIndex, 100, true, _metastreamMaxCount,
				Opts.HashCollisionReadLimitDefault, Opts.SkipIndexScanOnReadsDefault,
				_dbResult.Db.Config.ReplicationCheckpoint);
			ReadIndex.Init(_dbResult.Db.Config.WriterCheckpoint.Read());

			var scavenger = new TFChunkScavenger(_dbResult.Db, new FakeTFScavengerLog(), tableIndex, ReadIndex,
				unsafeIgnoreHardDeletes: UnsafeIgnoreHardDelete());
			scavenger.Scavenge(alwaysKeepScavenged: true, mergeChunks: false).Wait();
		}

		public override void TestFixtureTearDown() {
			ReadIndex.Close();
			_dbResult.Db.Close();

			base.TestFixtureTearDown();

			if (!_checked)
				throw new Exception("Records were not checked. Probably you forgot to call CheckRecords() method.");
		}

		protected abstract DbResult CreateDb(TFChunkDbCreationHelper dbCreator);

		protected abstract LogRecord[][] KeptRecords(DbResult dbResult);

		protected void CheckRecords() {
			_checked = true;
			Assert.AreEqual(_keptRecords.Length, _dbResult.Db.Manager.ChunksCount, "Wrong chunks count.");

			for (int i = 0; i < _keptRecords.Length; ++i) {
				var chunk = _dbResult.Db.Manager.GetChunk(i);

				var chunkRecords = new List<LogRecord>();
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
