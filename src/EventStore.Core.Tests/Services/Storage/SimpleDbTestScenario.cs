﻿using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.DataStructures;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.Hashes;
using EventStore.Core.TransactionLog.TestHelpers;
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage {
	[TestFixture]
	public abstract class SimpleDbTestScenario : SpecificationWithDirectoryPerTestFixture {
		protected readonly int MaxEntriesInMemTable;
		protected TableIndex TableIndex;
		protected IReadIndex ReadIndex;

		protected DbResult DbRes;

		protected abstract DbResult CreateDb(TFChunkDbCreationHelper dbCreator);

		private readonly long _metastreamMaxCount;

		protected SimpleDbTestScenario(int maxEntriesInMemTable = 20, long metastreamMaxCount = 1) {
			Ensure.Positive(maxEntriesInMemTable, "maxEntriesInMemTable");
			MaxEntriesInMemTable = maxEntriesInMemTable;
			_metastreamMaxCount = metastreamMaxCount;
		}

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			var dbConfig = TFChunkHelper.CreateDbConfig(PathName, 0, chunkSize: 1024 * 1024);
			var dbCreationHelper = new TFChunkDbCreationHelper(dbConfig);

			DbRes = CreateDb(dbCreationHelper);

			DbRes.Db.Config.WriterCheckpoint.Flush();
			DbRes.Db.Config.ChaserCheckpoint.Write(DbRes.Db.Config.WriterCheckpoint.Read());
			DbRes.Db.Config.ChaserCheckpoint.Flush();

			var readers = new ObjectPool<ITransactionFileReader>(
				"Readers", 2, 2, () => new TFChunkReader(DbRes.Db, DbRes.Db.Config.WriterCheckpoint));

			var lowHasher = new XXHashUnsafe();
			var highHasher = new Murmur3AUnsafe();
			TableIndex = new TableIndex(GetFilePathFor("index"), lowHasher, highHasher,
				() => new HashListMemTable(PTableVersions.IndexV2, MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				PTableVersions.IndexV2,
				int.MaxValue,
				Constants.PTableMaxReaderCountDefault,
				MaxEntriesInMemTable);

			ReadIndex = new ReadIndex(new NoopPublisher(),
				readers,
				TableIndex,
				0,
				additionalCommitChecks: true,
				metastreamMaxCount: _metastreamMaxCount,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: DbRes.Db.Config.ReplicationCheckpoint,
				indexCheckpoint: DbRes.Db.Config.IndexCheckpoint);

			((ReadIndex)ReadIndex).IndexCommitter.Init(DbRes.Db.Config.ChaserCheckpoint.Read());
		}

		public override Task TestFixtureTearDown() {
			DbRes.Db.Close();

			return base.TestFixtureTearDown();
		}
	}
}
