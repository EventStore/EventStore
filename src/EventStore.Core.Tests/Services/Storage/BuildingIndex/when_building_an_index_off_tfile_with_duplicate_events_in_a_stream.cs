using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Index;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Util;
using System;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.DataStructures;
using EventStore.Core.TransactionLog.Hashes;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.TestHelpers;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.BuildingIndex {
	[TestFixture]
	public class when_building_an_index_off_tfile_with_duplicate_events_in_a_stream : DuplicateReadIndexTestScenario {
		private Guid _id1;
		private Guid _id2;
		private Guid _id3;
		private Guid _id4;

		private long pos1, pos2, pos3, pos4, pos5, pos6, pos7;

		public when_building_an_index_off_tfile_with_duplicate_events_in_a_stream() : base(maxEntriesInMemTable: 3) {
		}

		protected override void SetupDB() {
			_id1 = Guid.NewGuid();
			_id2 = Guid.NewGuid();
			_id3 = Guid.NewGuid();

			//stream id: duplicate_stream at version: 0
			Writer.Write(new PrepareLogRecord(0, _id1, _id1, 0, 0, "duplicate_stream", ExpectedVersion.Any,
				DateTime.UtcNow,
				PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]), out pos1);
			Writer.Write(new CommitLogRecord(pos1, _id1, 0, DateTime.UtcNow, 0), out pos2);

			//stream id: duplicate_stream at version: 1
			Writer.Write(new PrepareLogRecord(pos2, _id2, _id2, pos2, 0, "duplicate_stream", ExpectedVersion.Any,
				DateTime.UtcNow,
				PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]), out pos3);
			Writer.Write(new CommitLogRecord(pos3, _id2, pos2, DateTime.UtcNow, 1), out pos4);

			//stream id: duplicate_stream at version: 2
			Writer.Write(new PrepareLogRecord(pos4, _id3, _id3, pos4, 0, "duplicate_stream", ExpectedVersion.Any,
				DateTime.UtcNow,
				PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]), out pos5);
			Writer.Write(new CommitLogRecord(pos5, _id3, pos4, DateTime.UtcNow, 2), out pos6);
		}

		protected override void Given() {
			_id4 = Guid.NewGuid();
			long pos8;

			//stream id: duplicate_stream at version: 0 (duplicate event/index entry)
			Writer.Write(new PrepareLogRecord(pos6, _id4, _id4, pos6, 0, "duplicate_stream", ExpectedVersion.Any,
				DateTime.UtcNow,
				PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]), out pos7);
			Writer.Write(new CommitLogRecord(pos7, _id4, pos6, DateTime.UtcNow, 0), out pos8);
		}

		[Test]
		public void should_read_the_correct_last_event_number() {
			var result = ReadIndex.GetStreamLastEventNumber("duplicate_stream");
			Assert.AreEqual(2, result);
		}
	}

	public abstract class DuplicateReadIndexTestScenario : SpecificationWithDirectoryPerTestFixture {
		protected readonly int MaxEntriesInMemTable;
		protected readonly int MetastreamMaxCount;
		protected readonly bool PerformAdditionalCommitChecks;
		protected readonly byte IndexBitnessVersion;
		protected TFChunkWriter Writer;
		protected IReadIndex ReadIndex;

		private TFChunkDb _db;
		private TableIndex _tableIndex;

		protected DuplicateReadIndexTestScenario(int maxEntriesInMemTable = 20, int metastreamMaxCount = 1,
			byte indexBitnessVersion = Opts.IndexBitnessVersionDefault, bool performAdditionalChecks = false) {
			Ensure.Positive(maxEntriesInMemTable, "maxEntriesInMemTable");
			MaxEntriesInMemTable = maxEntriesInMemTable;
			MetastreamMaxCount = metastreamMaxCount;
			IndexBitnessVersion = indexBitnessVersion;
			PerformAdditionalCommitChecks = performAdditionalChecks;
		}

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			var writerCheckpoint = new InMemoryCheckpoint(0);
			var chaserCheckpoint = new InMemoryCheckpoint(0);

			var bus = new InMemoryBus("bus");
			new IODispatcher(bus, new PublishEnvelope(bus));

			_db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, writerCheckpoint, chaserCheckpoint));

			_db.Open();
			// create db
			Writer = new TFChunkWriter(_db);
			Writer.Open();
			SetupDB();
			Writer.Close();
			Writer = null;

			writerCheckpoint.Flush();
			chaserCheckpoint.Write(writerCheckpoint.Read());
			chaserCheckpoint.Flush();

			var readers = new ObjectPool<ITransactionFileReader>("Readers", 2, 5,
				() => new TFChunkReader(_db, _db.Config.WriterCheckpoint));
			var lowHasher = new XXHashUnsafe();
			var highHasher = new Murmur3AUnsafe();
			_tableIndex = new TableIndex(GetFilePathFor("index"), lowHasher, highHasher,
				() => new HashListMemTable(IndexBitnessVersion, MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				IndexBitnessVersion,
				int.MaxValue,
				Constants.PTableMaxReaderCountDefault,
				MaxEntriesInMemTable);

			ReadIndex = new ReadIndex(new NoopPublisher(),
				readers,
				_tableIndex,
				EventStore.Common.Settings.ESConsts.StreamInfoCacheCapacity,
				additionalCommitChecks: PerformAdditionalCommitChecks,
				metastreamMaxCount: MetastreamMaxCount,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: _db.Config.ReplicationCheckpoint,
				indexCheckpoint: _db.Config.IndexCheckpoint);


			((ReadIndex)ReadIndex).IndexCommitter.Init(chaserCheckpoint.Read());

			_tableIndex.Close(false);

			Writer = new TFChunkWriter(_db);
			Writer.Open();
			Given();
			Writer.Close();
			Writer = null;

			writerCheckpoint.Flush();
			chaserCheckpoint.Write(writerCheckpoint.Read());
			chaserCheckpoint.Flush();

			_tableIndex = new TableIndex(GetFilePathFor("index"), lowHasher, highHasher,
				() => new HashListMemTable(IndexBitnessVersion, MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				IndexBitnessVersion,
				int.MaxValue,
				Constants.PTableMaxReaderCountDefault,
				MaxEntriesInMemTable);

			ReadIndex = new ReadIndex(new NoopPublisher(),
				readers,
				_tableIndex,
				EventStore.Common.Settings.ESConsts.StreamInfoCacheCapacity,
				additionalCommitChecks: PerformAdditionalCommitChecks,
				metastreamMaxCount: MetastreamMaxCount,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: _db.Config.ReplicationCheckpoint,
				indexCheckpoint: _db.Config.IndexCheckpoint);

			((ReadIndex)ReadIndex).IndexCommitter.Init(chaserCheckpoint.Read());
		}

		public override Task TestFixtureTearDown() {
			ReadIndex.Close();
			ReadIndex.Dispose();

			_tableIndex.Close();

			_db.Close();
			_db.Dispose();

			return base.TestFixtureTearDown();
		}

		protected abstract void SetupDB();
		protected abstract void Given();
	}
}
