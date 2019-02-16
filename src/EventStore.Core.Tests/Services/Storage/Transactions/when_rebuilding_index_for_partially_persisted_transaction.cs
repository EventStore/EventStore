using System;
using System.IO;
using System.Text;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using EventStore.Core.Util;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.Tests.Services.Storage.Transactions {
	[TestFixture]
	public class when_rebuilding_index_for_partially_persisted_transaction : ReadIndexTestScenario {
		public when_rebuilding_index_for_partially_persisted_transaction() : base(maxEntriesInMemTable: 10) {
		}

		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			ReadIndex.Close();
			ReadIndex.Dispose();
			TableIndex.Close(removeFiles: false);

			var readers =
				new ObjectPool<ITransactionFileReader>("Readers", 2, 2, () => new TFChunkReader(Db, WriterCheckpoint));
			var lowHasher = new XXHashUnsafe();
			var highHasher = new Murmur3AUnsafe();
			TableIndex = new TableIndex(GetFilePathFor("index"), lowHasher, highHasher,
				() => new HashListMemTable(PTableVersions.IndexV2, maxSize: MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				PTableVersions.IndexV2,
				5,
				maxSizeForMemory: MaxEntriesInMemTable);
			ReadIndex = new ReadIndex(new NoopPublisher(),
				readers,
				TableIndex,
				0,
				additionalCommitChecks: true,
				metastreamMaxCount: 1,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: Db.Config.ReplicationCheckpoint);
			ReadIndex.Init(ChaserCheckpoint.Read());
		}

		protected override void WriteTestScenario() {
			var begin = WriteTransactionBegin("ES", ExpectedVersion.Any);
			for (int i = 0; i < 15; ++i) {
				WriteTransactionEvent(Guid.NewGuid(), begin.LogPosition, i, "ES", i, "data" + i, PrepareFlags.Data);
			}

			WriteTransactionEnd(Guid.NewGuid(), begin.LogPosition, "ES");
			WriteCommit(Guid.NewGuid(), begin.LogPosition, "ES", 0);
		}

		[Test]
		public void sequence_numbers_are_not_broken() {
			for (int i = 0; i < 15; ++i) {
				var result = ReadIndex.ReadEvent("ES", i);
				Assert.AreEqual(ReadEventResult.Success, result.Result);
				Assert.AreEqual(Helper.UTF8NoBom.GetBytes("data" + i), result.Record.Data);
			}
		}
	}
}
