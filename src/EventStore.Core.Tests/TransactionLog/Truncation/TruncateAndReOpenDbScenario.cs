﻿using System.IO;
using System.Threading.Tasks;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.DataStructures;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.Hashes;
using EventStore.Core.TransactionLog.TestHelpers;
using EventStore.Core.Util;

namespace EventStore.Core.Tests.TransactionLog.Truncation {
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
