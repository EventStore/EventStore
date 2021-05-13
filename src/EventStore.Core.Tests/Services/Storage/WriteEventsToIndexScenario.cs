using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage {
	[TestFixture]
	public abstract class WriteEventsToIndexScenario<TLogFormat, TStreamId> {
		protected InMemoryBus _publisher;
		protected ITransactionFileReader _tfReader;
		protected ITableIndex<TStreamId> _tableIndex;
		protected IIndexBackend<TStreamId> _indexBackend;
		protected IStreamIdLookup<TStreamId> _streamIds;
		protected IStreamNameLookup<TStreamId> _streamNames;
		protected ISystemStreamLookup<TStreamId> _systemStreams;
		protected IStreamNamesProvider<TStreamId> _factory;
		protected IValidator<TStreamId> _validator;
		protected ISizer<TStreamId> _sizer;
		protected IIndexReader<TStreamId> _indexReader;
		protected IIndexWriter<TStreamId> _indexWriter;
		protected IIndexCommitter<TStreamId> _indexCommitter;
		protected ObjectPool<ITransactionFileReader> _readerPool;
		protected LogFormatAbstractor<TStreamId> _logFormat;
		protected const int RecordOffset = 1000;
		public IList<IPrepareLogRecord<TStreamId>> CreatePrepareLogRecord(string stream, int expectedVersion, string eventType, Guid eventId, long transactionPosition){
			_logFormat.StreamNameIndex.GetOrAddId(stream, out var streamId, out _, out _);
			return new[]{
				PrepareLogRecord.SingleWrite (
					_logFormat.RecordFactory,
					transactionPosition,
					Guid.NewGuid(),
					eventId,
					streamId,
					expectedVersion,
					eventType,
					new byte[0],
					new byte[0],
					DateTime.Now,
					PrepareFlags.IsCommitted
				)
			};
		}

		public IList<IPrepareLogRecord<TStreamId>> CreatePrepareLogRecords(string stream, int expectedVersion, IList<string> eventTypes, IList<Guid> eventIds, long transactionPosition){
			if(eventIds.Count != eventTypes.Count)
				throw new Exception("eventType and eventIds length mismatch!");
			if(eventIds.Count == 0)
				throw new Exception("eventIds is empty");
			if(eventIds.Count == 1)
				return CreatePrepareLogRecord(stream, expectedVersion, eventTypes[0], eventIds[0], transactionPosition);

			var numEvents = eventTypes.Count;
			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;

			var prepares = new List<IPrepareLogRecord<TStreamId>>();
			logFormat.StreamNameIndex.GetOrAddId(stream, out var streamId, out _, out _);
			for(var i=0;i<numEvents;i++){
				PrepareFlags flags = PrepareFlags.Data | PrepareFlags.IsCommitted;
				if(i==0) flags |= PrepareFlags.TransactionBegin;
				if(i==numEvents-1) flags |= PrepareFlags.TransactionEnd;

				prepares.Add(
					PrepareLogRecord.Prepare(
						logFormat.RecordFactory,
						transactionPosition + RecordOffset * i,
						Guid.NewGuid(),
						eventIds[i],
						transactionPosition,
						i,
						streamId,
						expectedVersion + i,
						flags,
						eventTypes[i],
						new byte[0],
						new byte[0],
						DateTime.Now
				));
			}

			return prepares;
		}

		public CommitLogRecord CreateCommitLogRecord(long logPosition, long transactionPosition, long firstEventNumber){
			return new CommitLogRecord (logPosition, Guid.NewGuid(), transactionPosition, DateTime.Now, 0);
		}

		public void WriteToDB(IList<IPrepareLogRecord<TStreamId>> prepares){
			foreach(var prepare in prepares){
				((FakeInMemoryTfReader)_tfReader).AddRecord(prepare, prepare.LogPosition);
			}
		}

		public void WriteToDB(CommitLogRecord commit){
			((FakeInMemoryTfReader)_tfReader).AddRecord(commit, commit.LogPosition);
		}

		public void PreCommitToIndex(IList<IPrepareLogRecord<TStreamId>> prepares){
			_indexWriter.PreCommit(prepares);
		}

		public void PreCommitToIndex(CommitLogRecord commitLogRecord){
			_indexWriter.PreCommit(commitLogRecord);
		}

		public void CommitToIndex(IList<IPrepareLogRecord<TStreamId>> prepares){
			_indexCommitter.Commit(prepares, false, false);
		}

		public void CommitToIndex(CommitLogRecord commitLogRecord){
			_indexCommitter.Commit(commitLogRecord, false, false);
		}

		public abstract void WriteEvents();

        [OneTimeSetUp]
		public virtual void TestFixtureSetUp() {
			_logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
			_publisher = new InMemoryBus("publisher");
			_tfReader = new FakeInMemoryTfReader(RecordOffset);
			_tableIndex = new FakeInMemoryTableIndex<TStreamId>();
			_readerPool = new ObjectPool<ITransactionFileReader>(
				"ReadIndex readers pool", 5, 100,
				() => _tfReader);
			_indexBackend = new IndexBackend<TStreamId>(_readerPool, 100000, 100000);
			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
			_streamIds = logFormat.StreamIds;
			_streamNames = logFormat.StreamNames;
			_systemStreams = logFormat.SystemStreams;
			_factory = logFormat.StreamNamesProvider;
			_validator = logFormat.StreamIdValidator;
			var emptyStreamId = logFormat.EmptyStreamId;
			_sizer = logFormat.StreamIdSizer;
			_indexReader = new IndexReader<TStreamId>(_indexBackend, _tableIndex, _factory, _validator, new StreamMetadata(maxCount: 100000), 100, false);
			_indexWriter = new IndexWriter<TStreamId>(_indexBackend, _indexReader, _streamIds, _streamNames, _systemStreams, emptyStreamId, _sizer);
			_indexCommitter = new Core.Services.Storage.ReaderIndex.IndexCommitter<TStreamId>(_publisher, _indexBackend, _indexReader, _tableIndex, _streamNames, _systemStreams, new InMemoryCheckpoint(-1),  false);

			WriteEvents();
		}

		[OneTimeTearDown]
		public virtual void TestFixtureTearDown() {
			_readerPool.Dispose();
		}
	}
}
