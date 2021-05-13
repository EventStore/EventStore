using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Helpers;
using EventStore.Core.Index;
using EventStore.Core.LogAbstraction;
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
using EventStore.LogCommon;

namespace EventStore.Core.Tests.Services.Storage {
	public abstract class ReadIndexTestScenario<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		protected readonly int MaxEntriesInMemTable;
		protected readonly long MetastreamMaxCount;
		protected readonly bool PerformAdditionalCommitChecks;
		protected readonly byte IndexBitnessVersion;
		protected readonly LogFormatAbstractor<TStreamId> _logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
		protected readonly IRecordFactory<TStreamId> _recordFactory = LogFormatHelper<TLogFormat, TStreamId>.LogFormat.RecordFactory;
		protected readonly IStreamNameIndex<TStreamId> _streamNameIndex = LogFormatHelper<TLogFormat, TStreamId>.LogFormat.StreamNameIndex;
		protected TableIndex<TStreamId> TableIndex;
		protected ITestReadIndex<TStreamId> ReadIndex;

		protected TFChunkDb Db;
		protected TFChunkWriter Writer;
		protected ICheckpoint WriterCheckpoint;
		protected ICheckpoint ChaserCheckpoint;

		private TFChunkScavenger<TStreamId> _scavenger;
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

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			WriterCheckpoint = new InMemoryCheckpoint(0);
			ChaserCheckpoint = new InMemoryCheckpoint(0);

			Db = new TFChunkDb(TFChunkHelper.CreateDbConfig(PathName, WriterCheckpoint, ChaserCheckpoint,
				replicationCheckpoint: new InMemoryCheckpoint(-1)));

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

			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
			var readers = new ObjectPool<ITransactionFileReader>("Readers", 2, 5,
				() => new TFChunkReader(Db, Db.Config.WriterCheckpoint));
			var lowHasher = logFormat.LowHasher;
			var highHasher = logFormat.HighHasher;
			var emptyStreamId = logFormat.EmptyStreamId;
			TableIndex = new TableIndex<TStreamId>(GetFilePathFor("index"), lowHasher, highHasher, emptyStreamId,
				() => new HashListMemTable(IndexBitnessVersion, MaxEntriesInMemTable * 2),
				() => new TFReaderLease(readers),
				IndexBitnessVersion,
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
				additionalCommitChecks: PerformAdditionalCommitChecks,
				metastreamMaxCount: MetastreamMaxCount,
				hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault,
				skipIndexScanOnReads: Opts.SkipIndexScanOnReadsDefault,
				replicationCheckpoint: Db.Config.ReplicationCheckpoint,
				indexCheckpoint: Db.Config.IndexCheckpoint);

			readIndex.IndexCommitter.Init(ChaserCheckpoint.Read());
			ReadIndex = new TestReadIndex<TStreamId>(readIndex, _streamNameIndex);

			// scavenge must run after readIndex is built
			if (_scavenge) {
				if (_completeLastChunkOnScavenge)
					Db.Manager.GetChunk(Db.Manager.ChunksCount - 1).Complete();
				_scavenger = new TFChunkScavenger<TStreamId>(Db, new FakeTFScavengerLog(), TableIndex, ReadIndex, logFormat.SystemStreams);
				await _scavenger.Scavenge(alwaysKeepScavenged: true, mergeChunks: _mergeChunks);
			}
		}

		public override Task TestFixtureTearDown() {
			ReadIndex.Close();
			ReadIndex.Dispose();

			TableIndex.Close();

			Db.Close();
			Db.Dispose();

			return base.TestFixtureTearDown();
		}

		protected abstract void WriteTestScenario();

		protected EventRecord WriteSingleEvent(string eventStreamName,
			long eventNumber,
			string data,
			DateTime? timestamp = null,
			Guid eventId = default(Guid),
			bool retryOnFail = false,
			string eventType = "some-type") {
			_streamNameIndex.GetOrAddId(eventStreamName, out var eventStreamId, out _, out _);
			var prepare = LogRecord.SingleWrite(_recordFactory, WriterCheckpoint.ReadNonFlushed(),
				eventId == default(Guid) ? Guid.NewGuid() : eventId,
				Guid.NewGuid(),
				eventStreamId,
				eventNumber - 1,
				eventType,
				Helper.UTF8NoBom.GetBytes(data),
				null,
				timestamp);
			long pos;

			if (!retryOnFail) {
				Assert.IsTrue(Writer.Write(prepare, out pos));
			} else {
				long firstPos = prepare.LogPosition;
				if (!Writer.Write(prepare, out pos)) {
					prepare = LogRecord.SingleWrite(_recordFactory, pos,
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
			Assert.AreEqual(eventStreamId, prepare.EventStreamId);

			var eventRecord = new EventRecord(eventNumber, prepare, eventStreamName);
			return eventRecord;
		}

		protected EventRecord WriteStreamMetadata(string eventStreamName, long eventNumber, string metadata,
			DateTime? timestamp = null) {
			_streamNameIndex.GetOrAddId(SystemStreams.MetastreamOf(eventStreamName), out var eventStreamId, out _, out _);
			var prepare = LogRecord.SingleWrite(_recordFactory, WriterCheckpoint.ReadNonFlushed(),
				Guid.NewGuid(),
				Guid.NewGuid(),
				eventStreamId,
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
			Assert.AreEqual(eventStreamId, prepare.EventStreamId);

			var eventRecord = new EventRecord(eventNumber, prepare, SystemStreams.MetastreamOf(eventStreamName));
			return eventRecord;
		}

		protected EventRecord WriteTransactionBegin(string eventStreamName, long expectedVersion, long eventNumber,
			string eventData) {
			LogFormatHelper<TLogFormat, TStreamId>.CheckIfExplicitTransactionsSupported();
			_streamNameIndex.GetOrAddId(eventStreamName, out var eventStreamId, out _, out _);
			var prepare = LogRecord.Prepare(_recordFactory, WriterCheckpoint.ReadNonFlushed(),
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
			Assert.AreEqual(eventStreamId, prepare.EventStreamId);

			return new EventRecord(eventNumber, prepare, eventStreamName);
		}

		protected IPrepareLogRecord<TStreamId> WriteTransactionBegin(string eventStreamName, long expectedVersion) {
			LogFormatHelper<TLogFormat, TStreamId>.CheckIfExplicitTransactionsSupported();
			_streamNameIndex.GetOrAddId(eventStreamName, out var eventStreamId, out _, out _);
			var prepare = LogRecord.TransactionBegin(_recordFactory, WriterCheckpoint.ReadNonFlushed(), Guid.NewGuid(), eventStreamId,
				expectedVersion);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
			return prepare;
		}

		protected EventRecord WriteTransactionEvent(Guid correlationId,
			long transactionPos,
			int transactionOffset,
			string eventStreamName,
			long eventNumber,
			string eventData,
			PrepareFlags flags,
			bool retryOnFail = false) {
			LogFormatHelper<TLogFormat, TStreamId>.CheckIfExplicitTransactionsSupported();

			_streamNameIndex.GetOrAddId(eventStreamName, out var eventStreamId, out _, out _);

			var prepare = LogRecord.Prepare(_recordFactory, WriterCheckpoint.ReadNonFlushed(),
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
					prepare = _recordFactory.CopyForRetry(
						prepare: prepare,
						logPosition: newPos,
						transactionPosition: tPos);
					if (!Writer.Write(prepare, out newPos))
						Assert.Fail("Second write try failed when first writing prepare at {0}, then at {1}.", firstPos,
							prepare.LogPosition);
				}

				Assert.AreEqual(eventStreamId, prepare.EventStreamId);
				return new EventRecord(eventNumber, prepare, eventStreamName);
			}

			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));

			Assert.AreEqual(eventStreamId, prepare.EventStreamId);
			return new EventRecord(eventNumber, prepare, eventStreamName);
		}

		protected IPrepareLogRecord<TStreamId> WriteTransactionEnd(Guid correlationId, long transactionId, string eventStreamName) {
			LogFormatHelper<TLogFormat, TStreamId>.CheckIfExplicitTransactionsSupported();
			_streamNameIndex.GetOrAddId(eventStreamName, out var eventStreamId, out _, out _);
			return WriteTransactionEnd(correlationId, transactionId, eventStreamId);
		}

		protected IPrepareLogRecord<TStreamId> WriteTransactionEnd(Guid correlationId, long transactionId, TStreamId eventStreamId) {
			LogFormatHelper<TLogFormat, TStreamId>.CheckIfExplicitTransactionsSupported();
			var prepare = LogRecord.TransactionEnd(_recordFactory, WriterCheckpoint.ReadNonFlushed(),
				correlationId,
				Guid.NewGuid(),
				transactionId,
				eventStreamId);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
			return prepare;
		}

		protected IPrepareLogRecord<TStreamId> WritePrepare(string eventStreamName,
			long expectedVersion,
			Guid eventId = default(Guid),
			string eventType = null,
			string data = null,
			PrepareFlags additionalFlags = PrepareFlags.None) {
			_streamNameIndex.GetOrAddId(eventStreamName, out var eventStreamId, out _, out _);
			long pos;
			var prepare = LogRecord.SingleWrite(_recordFactory, WriterCheckpoint.ReadNonFlushed(),
				Guid.NewGuid(),
				eventId == default(Guid) ? Guid.NewGuid() : eventId,
				eventStreamId,
				expectedVersion,
				eventType.IsEmptyString() ? "some-type" : eventType,
				data.IsEmptyString() ? LogRecord.NoData : Helper.UTF8NoBom.GetBytes(data),
				LogRecord.NoData,
				DateTime.UtcNow,
				additionalFlags);
			Assert.IsTrue(Writer.Write(prepare, out pos));

			return prepare;
		}

		protected CommitLogRecord WriteCommit(long preparePos, string eventStreamName, long eventNumber) {
			LogFormatHelper<TLogFormat, TStreamId>.CheckIfExplicitTransactionsSupported();
			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(), Guid.NewGuid(), preparePos, eventNumber);
			long pos;
			Assert.IsTrue(Writer.Write(commit, out pos));
			return commit;
		}

		protected long WriteCommit(Guid correlationId, long transactionId, string eventStreamName, long eventNumber) {
			LogFormatHelper<TLogFormat, TStreamId>.CheckIfExplicitTransactionsSupported();
			_streamNameIndex.GetOrAddId(eventStreamName, out var eventStreamId, out _, out _);
			return WriteCommit(correlationId, transactionId, eventStreamId, eventNumber);
		}

		protected long WriteCommit(Guid correlationId, long transactionId, TStreamId eventStreamId, long eventNumber) {
			LogFormatHelper<TLogFormat, TStreamId>.CheckIfExplicitTransactionsSupported();
			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(), correlationId, transactionId, eventNumber);
			long pos;
			Assert.IsTrue(Writer.Write(commit, out pos));
			return commit.LogPosition;
		}

		protected EventRecord WriteDelete(string eventStreamName) {
			_streamNameIndex.GetOrAddId(eventStreamName, out var eventStreamId, out _, out _);
			var prepare = LogRecord.DeleteTombstone(_recordFactory, WriterCheckpoint.ReadNonFlushed(),
				Guid.NewGuid(), Guid.NewGuid(), eventStreamId, EventNumber.DeletedStream - 1);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(),
				prepare.CorrelationId,
				prepare.LogPosition,
				EventNumber.DeletedStream);
			Assert.IsTrue(Writer.Write(commit, out pos));
			Assert.AreEqual(eventStreamId, prepare.EventStreamId);

			return new EventRecord(EventNumber.DeletedStream, prepare, eventStreamName);
		}

		protected IPrepareLogRecord<TStreamId> WriteDeletePrepare(string eventStreamName) {
			_streamNameIndex.GetOrAddId(eventStreamName, out var eventStreamId, out _, out _);
			var prepare = LogRecord.DeleteTombstone(_recordFactory, WriterCheckpoint.ReadNonFlushed(),
				Guid.NewGuid(), Guid.NewGuid(), eventStreamId, ExpectedVersion.Any);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));

			return prepare;
		}

		protected CommitLogRecord WriteDeleteCommit(IPrepareLogRecord prepare) {
			LogFormatHelper<TLogFormat, TStreamId>.CheckIfExplicitTransactionsSupported();
			long pos;
			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(),
				prepare.CorrelationId,
				prepare.LogPosition,
				EventNumber.DeletedStream);
			Assert.IsTrue(Writer.Write(commit, out pos));

			return commit;
		}

		// This is LogV2 specific
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
