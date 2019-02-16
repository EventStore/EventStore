using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Helpers;
using EventStore.Core.Index;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using EventStore.Core.Util;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;

namespace EventStore.Core.Tests.Services.Storage {
	public abstract class ReadIndexTestScenario : SpecificationWithDirectoryPerTestFixture {
		protected readonly int MaxEntriesInMemTable;
		protected readonly long MetastreamMaxCount;
		protected readonly bool PerformAdditionalCommitChecks;
		protected readonly byte IndexBitnessVersion;
		protected TableIndex TableIndex;
		protected IReadIndex ReadIndex;

		protected TFChunkDb Db;
		protected TFChunkWriter Writer;
		protected ICheckpoint WriterCheckpoint;
		protected ICheckpoint ChaserCheckpoint;
		protected ICheckpoint ReplicationCheckpoint;

		private TFChunkScavenger _scavenger;
		private bool _scavenge;
		private bool _completeLastChunkOnScavenge;
		private bool _mergeChunks;

		protected ReadIndexTestScenario(int maxEntriesInMemTable = 20, long metastreamMaxCount = 1,
			byte indexBitnessVersion = Opts.IndexBitnessVersionDefault, bool performAdditionalChecks = true) {
			Ensure.Positive(maxEntriesInMemTable, "maxEntriesInMemTable");
			MaxEntriesInMemTable = maxEntriesInMemTable;
			MetastreamMaxCount = metastreamMaxCount;
			IndexBitnessVersion = indexBitnessVersion;
			PerformAdditionalCommitChecks = performAdditionalChecks;
		}

		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			WriterCheckpoint = new InMemoryCheckpoint(0);
			ChaserCheckpoint = new InMemoryCheckpoint(0);
			ReplicationCheckpoint = new InMemoryCheckpoint(-1);

			Db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, WriterCheckpoint, ChaserCheckpoint,
				replicationCheckpoint: ReplicationCheckpoint));

			Db.Open();
			// create db
			Writer = new TFChunkWriter(Db);
			Writer.Open();
			WriteTestScenario();
			Writer.Close();
			Writer = null;

			WriterCheckpoint.Flush();
			ChaserCheckpoint.Write(WriterCheckpoint.Read());
			ChaserCheckpoint.Flush();

			var readers = new ObjectPool<ITransactionFileReader>("Readers", 2, 5,
				() => new TFChunkReader(Db, Db.Config.WriterCheckpoint));
			var lowHasher = new XXHashUnsafe();
			var highHasher = new Murmur3AUnsafe();
			TableIndex = new TableIndex(GetFilePathFor("index"), lowHasher, highHasher,
				() => new HashListMemTable(IndexBitnessVersion, MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				IndexBitnessVersion,
				MaxEntriesInMemTable);

			ReadIndex = new ReadIndex(new NoopPublisher(),
				readers,
				TableIndex,
				0,
				additionalCommitChecks: PerformAdditionalCommitChecks,
				metastreamMaxCount: MetastreamMaxCount,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: Db.Config.ReplicationCheckpoint);

			ReadIndex.Init(ChaserCheckpoint.Read());

			// scavenge must run after readIndex is built
			if (_scavenge) {
				if (_completeLastChunkOnScavenge)
					Db.Manager.GetChunk(Db.Manager.ChunksCount - 1).Complete();
				_scavenger = new TFChunkScavenger(Db, new FakeTFScavengerLog(), TableIndex, ReadIndex);
				_scavenger.Scavenge(alwaysKeepScavenged: true, mergeChunks: _mergeChunks).Wait();
			}
		}

		public override void TestFixtureTearDown() {
			ReadIndex.Close();
			ReadIndex.Dispose();

			TableIndex.Close();

			Db.Close();
			Db.Dispose();

			base.TestFixtureTearDown();
		}

		protected abstract void WriteTestScenario();

		protected EventRecord WriteSingleEvent(string eventStreamId,
			long eventNumber,
			string data,
			DateTime? timestamp = null,
			Guid eventId = default(Guid),
			bool retryOnFail = false) {
			var prepare = LogRecord.SingleWrite(WriterCheckpoint.ReadNonFlushed(),
				eventId == default(Guid) ? Guid.NewGuid() : eventId,
				Guid.NewGuid(),
				eventStreamId,
				eventNumber - 1,
				"some-type",
				Helper.UTF8NoBom.GetBytes(data),
				null,
				timestamp);
			long pos;

			if (!retryOnFail) {
				Assert.IsTrue(Writer.Write(prepare, out pos));
			} else {
				long firstPos = prepare.LogPosition;
				if (!Writer.Write(prepare, out pos)) {
					prepare = LogRecord.SingleWrite(pos,
						prepare.CorrelationId,
						prepare.EventId,
						prepare.EventStreamId,
						prepare.ExpectedVersion,
						prepare.EventType,
						prepare.Data,
						prepare.Metadata,
						prepare.TimeStamp);
					if (!Writer.Write(prepare, out pos))
						Assert.Fail("Second write try failed when first writing prepare at {0}, then at {1}.", firstPos,
							prepare.LogPosition);
				}
			}

			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(), prepare.CorrelationId, prepare.LogPosition,
				eventNumber);
			Assert.IsTrue(Writer.Write(commit, out pos));

			var eventRecord = new EventRecord(eventNumber, prepare);
			return eventRecord;
		}

		protected EventRecord WriteStreamMetadata(string eventStreamId, long eventNumber, string metadata,
			DateTime? timestamp = null) {
			var prepare = LogRecord.SingleWrite(WriterCheckpoint.ReadNonFlushed(),
				Guid.NewGuid(),
				Guid.NewGuid(),
				SystemStreams.MetastreamOf(eventStreamId),
				eventNumber - 1,
				SystemEventTypes.StreamMetadata,
				Helper.UTF8NoBom.GetBytes(metadata),
				null,
				timestamp ?? DateTime.UtcNow,
				PrepareFlags.IsJson);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));

			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(), prepare.CorrelationId, prepare.LogPosition,
				eventNumber);
			Assert.IsTrue(Writer.Write(commit, out pos));

			var eventRecord = new EventRecord(eventNumber, prepare);
			return eventRecord;
		}

		protected EventRecord WriteTransactionBegin(string eventStreamId, long expectedVersion, long eventNumber,
			string eventData) {
			var prepare = LogRecord.Prepare(WriterCheckpoint.ReadNonFlushed(),
				Guid.NewGuid(),
				Guid.NewGuid(),
				WriterCheckpoint.ReadNonFlushed(),
				0,
				eventStreamId,
				expectedVersion,
				PrepareFlags.Data | PrepareFlags.TransactionBegin,
				"some-type",
				Helper.UTF8NoBom.GetBytes(eventData),
				null);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
			return new EventRecord(eventNumber, prepare);
		}

		protected PrepareLogRecord WriteTransactionBegin(string eventStreamId, long expectedVersion) {
			var prepare = LogRecord.TransactionBegin(WriterCheckpoint.ReadNonFlushed(), Guid.NewGuid(), eventStreamId,
				expectedVersion);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
			return prepare;
		}

		protected EventRecord WriteTransactionEvent(Guid correlationId,
			long transactionPos,
			int transactionOffset,
			string eventStreamId,
			long eventNumber,
			string eventData,
			PrepareFlags flags,
			bool retryOnFail = false) {
			var prepare = LogRecord.Prepare(WriterCheckpoint.ReadNonFlushed(),
				correlationId,
				Guid.NewGuid(),
				transactionPos,
				transactionOffset,
				eventStreamId,
				ExpectedVersion.Any,
				flags,
				"some-type",
				Helper.UTF8NoBom.GetBytes(eventData),
				null);

			if (retryOnFail) {
				long firstPos = prepare.LogPosition;
				long newPos;
				if (!Writer.Write(prepare, out newPos)) {
					var tPos = prepare.TransactionPosition == prepare.LogPosition
						? newPos
						: prepare.TransactionPosition;
					prepare = new PrepareLogRecord(newPos,
						prepare.CorrelationId,
						prepare.EventId,
						tPos,
						prepare.TransactionOffset,
						prepare.EventStreamId,
						prepare.ExpectedVersion,
						prepare.TimeStamp,
						prepare.Flags,
						prepare.EventType,
						prepare.Data,
						prepare.Metadata);
					if (!Writer.Write(prepare, out newPos))
						Assert.Fail("Second write try failed when first writing prepare at {0}, then at {1}.", firstPos,
							prepare.LogPosition);
				}

				return new EventRecord(eventNumber, prepare);
			}

			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
			return new EventRecord(eventNumber, prepare);
		}

		protected PrepareLogRecord WriteTransactionEnd(Guid correlationId, long transactionId, string eventStreamId) {
			var prepare = LogRecord.TransactionEnd(WriterCheckpoint.ReadNonFlushed(),
				correlationId,
				Guid.NewGuid(),
				transactionId,
				eventStreamId);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
			return prepare;
		}

		protected PrepareLogRecord WritePrepare(string streamId,
			long expectedVersion,
			Guid eventId = default(Guid),
			string eventType = null,
			string data = null) {
			long pos;
			var prepare = LogRecord.SingleWrite(WriterCheckpoint.ReadNonFlushed(),
				Guid.NewGuid(),
				eventId == default(Guid) ? Guid.NewGuid() : eventId,
				streamId,
				expectedVersion,
				eventType.IsEmptyString() ? "some-type" : eventType,
				data.IsEmptyString() ? LogRecord.NoData : Helper.UTF8NoBom.GetBytes(data),
				LogRecord.NoData,
				DateTime.UtcNow);
			Assert.IsTrue(Writer.Write(prepare, out pos));

			return prepare;
		}

		protected CommitLogRecord WriteCommit(long preparePos, string eventStreamId, long eventNumber) {
			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(), Guid.NewGuid(), preparePos, eventNumber);
			long pos;
			Assert.IsTrue(Writer.Write(commit, out pos));
			return commit;
		}

		protected long WriteCommit(Guid correlationId, long transactionId, string eventStreamId, long eventNumber) {
			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(), correlationId, transactionId, eventNumber);
			long pos;
			Assert.IsTrue(Writer.Write(commit, out pos));
			return commit.LogPosition;
		}

		protected EventRecord WriteDelete(string eventStreamId) {
			var prepare = LogRecord.DeleteTombstone(WriterCheckpoint.ReadNonFlushed(),
				Guid.NewGuid(), Guid.NewGuid(), eventStreamId, ExpectedVersion.Any);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(),
				prepare.CorrelationId,
				prepare.LogPosition,
				EventNumber.DeletedStream);
			Assert.IsTrue(Writer.Write(commit, out pos));

			return new EventRecord(EventNumber.DeletedStream, prepare);
		}

		protected PrepareLogRecord WriteDeletePrepare(string eventStreamId) {
			var prepare = LogRecord.DeleteTombstone(WriterCheckpoint.ReadNonFlushed(),
				Guid.NewGuid(), Guid.NewGuid(), eventStreamId, ExpectedVersion.Any);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));

			return prepare;
		}

		protected CommitLogRecord WriteDeleteCommit(PrepareLogRecord prepare) {
			long pos;
			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(),
				prepare.CorrelationId,
				prepare.LogPosition,
				EventNumber.DeletedStream);
			Assert.IsTrue(Writer.Write(commit, out pos));

			return commit;
		}

		protected PrepareLogRecord WriteSingleEventWithLogVersion0(Guid id, string streamId, long position,
			long expectedVersion, PrepareFlags? flags = null) {
			if (!flags.HasValue) {
				flags = PrepareFlags.SingleWrite;
			}

			long pos;
			var record = new PrepareLogRecord(position, id, id, position, 0, streamId, expectedVersion, DateTime.UtcNow,
				flags.Value, "type", new byte[10], new byte[0], LogRecordVersion.LogRecordV0);
			Writer.Write(record, out pos);
			Writer.Write(
				new CommitLogRecord(pos, id, position, DateTime.UtcNow, expectedVersion, LogRecordVersion.LogRecordV0),
				out pos);
			return record;
		}

		protected TFPos GetBackwardReadPos() {
			var pos = new TFPos(WriterCheckpoint.ReadNonFlushed(), WriterCheckpoint.ReadNonFlushed());
			return pos;
		}

		protected void Scavenge(bool completeLast, bool mergeChunks) {
			if (_scavenge)
				throw new InvalidOperationException("Scavenge can be executed only once in ReadIndexTestScenario");
			_scavenge = true;
			_completeLastChunkOnScavenge = completeLast;
			_mergeChunks = mergeChunks;
		}
	}
}
