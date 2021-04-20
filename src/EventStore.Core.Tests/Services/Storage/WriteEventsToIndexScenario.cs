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
	public abstract class WriteEventsToIndexScenario {
		protected InMemoryBus _publisher;
		protected ITransactionFileReader _tfReader;
		protected ITableIndex<string> _tableIndex;
		protected IIndexBackend<string> _indexBackend;
		protected IStreamIdLookup<string> _streamIds;
		protected IStreamNameLookup<string> _streamNames;
		protected ISystemStreamLookup<string> _systemStreams;
		protected IValidator<string> _validator;
		protected ISizer<string> _sizer;
		protected IIndexReader<string> _indexReader;
		protected IIndexWriter<string> _indexWriter;
		protected IIndexCommitter<string> _indexCommitter;
		protected ObjectPool<ITransactionFileReader> _readerPool;
		protected const int RecordOffset = 1000;
		public IList<IPrepareLogRecord<string>> CreatePrepareLogRecord(string stream, int expectedVersion, string eventType, Guid eventId, long transactionPosition){
			return new PrepareLogRecord[]{
				new PrepareLogRecord (
					transactionPosition,
					Guid.NewGuid(),
					eventId,
					transactionPosition,
					0,
					stream,
					expectedVersion,
					DateTime.Now,
					PrepareFlags.SingleWrite | PrepareFlags.IsCommitted,
					eventType,
					new byte[0],
					new byte[0]
				)
			};
		}

		public IList<IPrepareLogRecord<string>> CreatePrepareLogRecords(string stream, int expectedVersion, IList<string> eventTypes, IList<Guid> eventIds, long transactionPosition){
			if(eventIds.Count != eventTypes.Count)
				throw new Exception("eventType and eventIds length mismatch!");
			if(eventIds.Count == 0)
				throw new Exception("eventIds is empty");
			if(eventIds.Count == 1)
				return CreatePrepareLogRecord(stream, expectedVersion, eventTypes[0], eventIds[0], transactionPosition);

			var numEvents = eventTypes.Count;

			var prepares = new List<IPrepareLogRecord<string>>();
			for(var i=0;i<numEvents;i++){
				PrepareFlags flags = PrepareFlags.Data | PrepareFlags.IsCommitted;
				if(i==0) flags |= PrepareFlags.TransactionBegin;
				if(i==numEvents-1) flags |= PrepareFlags.TransactionEnd;

				prepares.Add(
					new PrepareLogRecord (
						transactionPosition + RecordOffset * i,
						Guid.NewGuid(),
						eventIds[i],
						transactionPosition,
						i,
						stream,
						expectedVersion+i,
						DateTime.Now,
						flags,
						eventTypes[i],
						new byte[0],
						new byte[0]
					)
				);
			}

			return prepares;
		}

		public CommitLogRecord CreateCommitLogRecord(long logPosition, long transactionPosition, long firstEventNumber){
			return new CommitLogRecord (logPosition, Guid.NewGuid(), transactionPosition, DateTime.Now, 0);
		}

		public void WriteToDB(IList<IPrepareLogRecord<string>> prepares){
			foreach(var prepare in prepares){
				((FakeInMemoryTfReader)_tfReader).AddRecord(prepare, prepare.LogPosition);
			}
		}

		public void WriteToDB(CommitLogRecord commit){
			((FakeInMemoryTfReader)_tfReader).AddRecord(commit, commit.LogPosition);
		}

		public void PreCommitToIndex(IList<IPrepareLogRecord<string>> prepares){
			_indexWriter.PreCommit(prepares);
		}

		public void PreCommitToIndex(CommitLogRecord commitLogRecord){
			_indexWriter.PreCommit(commitLogRecord);
		}

		public void CommitToIndex(IList<IPrepareLogRecord<string>> prepares){
			_indexCommitter.Commit(prepares, false, false);
		}

		public void CommitToIndex(CommitLogRecord commitLogRecord){
			_indexCommitter.Commit(commitLogRecord, false, false);
		}

		public abstract void WriteEvents();

        [OneTimeSetUp]
		public virtual void TestFixtureSetUp() {
			_publisher = new InMemoryBus("publisher");
			_tfReader = new FakeInMemoryTfReader(RecordOffset);
			_tableIndex = new FakeInMemoryTableIndex<string>();
			_readerPool = new ObjectPool<ITransactionFileReader>(
				"ReadIndex readers pool", 5, 100,
				() => _tfReader);
			_indexBackend = new IndexBackend<string>(_readerPool, 100000, 100000);
			var logFormat = LogFormatAbstractor.V2;
			_streamIds = logFormat.StreamIds;
			_streamNames = logFormat.StreamNamesFactory.Create();
			_systemStreams = logFormat.SystemStreams;
			_validator = logFormat.StreamIdValidator;
			var emptyStreamId = logFormat.EmptyStreamId;
			_sizer = logFormat.StreamIdSizer;
			_indexReader = new IndexReader<string>(_indexBackend, _tableIndex, _systemStreams, _validator, new StreamMetadata(maxCount: 100000), 100, false);
			_indexWriter = new IndexWriter<string>(_indexBackend, _indexReader, _streamIds, _streamNames, _systemStreams, emptyStreamId, _sizer);
			_indexCommitter = new Core.Services.Storage.ReaderIndex.IndexCommitter<string>(_publisher, _indexBackend, _indexReader, _tableIndex, _streamNames, _systemStreams, new InMemoryCheckpoint(-1),  false);

			WriteEvents();
		}

		[OneTimeTearDown]
		public virtual void TestFixtureTearDown() {
			_readerPool.Dispose();
		}
	}
}
