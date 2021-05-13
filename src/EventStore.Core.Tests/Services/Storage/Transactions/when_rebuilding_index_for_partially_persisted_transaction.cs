using System;
using System.Threading.Tasks;
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
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long), Ignore = "Explicit transactions are not supported yet by Log V3")]
	public class when_rebuilding_index_for_partially_persisted_transaction<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		public when_rebuilding_index_for_partially_persisted_transaction() : base(maxEntriesInMemTable: 10) {
		}

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			ReadIndex.Close();
			ReadIndex.Dispose();
			TableIndex.Close(removeFiles: false);

			var readers =
				new ObjectPool<ITransactionFileReader>("Readers", 2, 2, () => new TFChunkReader(Db, WriterCheckpoint));
			var lowHasher = _logFormat.LowHasher;
			var highHasher = _logFormat.HighHasher;
			var emptyStreamId = _logFormat.EmptyStreamId;
			TableIndex = new TableIndex<TStreamId>(GetFilePathFor("index"), lowHasher, highHasher, emptyStreamId,
				() => new HashListMemTable(PTableVersions.IndexV2, maxSize: MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				PTableVersions.IndexV2,
				5, Constants.PTableMaxReaderCountDefault,
				maxSizeForMemory: MaxEntriesInMemTable);
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
				metastreamMaxCount: 1,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: Db.Config.ReplicationCheckpoint,
				indexCheckpoint: Db.Config.IndexCheckpoint);
			readIndex.IndexCommitter.Init(ChaserCheckpoint.Read());
			ReadIndex = new TestReadIndex<TStreamId>(readIndex, _streamNameIndex);
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
				Assert.AreEqual(Helper.UTF8NoBom.GetBytes("data" + i), result.Record.Data.ToArray());
			}
		}
	}
}
