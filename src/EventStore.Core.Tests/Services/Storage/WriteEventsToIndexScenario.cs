using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage {
	[TestFixture]
	public abstract class WriteEventsToIndexScenario {
		protected InMemoryBus _publisher;
		protected ITransactionFileReader _tfReader;
		protected ITableIndex _tableIndex;
		protected IIndexBackend _indexBackend;
		protected IIndexReader _indexReader;
		protected IIndexWriter _indexWriter;
		protected IIndexCommitter _indexCommitter;
		protected ObjectPool<ITransactionFileReader> _readerPool;
		protected const int RecordOffset = 1000;
		public IList<PrepareLogRecord> CreatePrepareLogRecord(string stream, int expectedVersion, string eventType, Guid eventId, long transactionPosition){
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

		public IList<PrepareLogRecord> CreatePrepareLogRecords(string stream, int expectedVersion, IList<string> eventTypes, IList<Guid> eventIds, long transactionPosition){
			if(eventIds.Count != eventTypes.Count)
				throw new Exception("eventType and eventIds length mismatch!");
			if(eventIds.Count == 0)
				throw new Exception("eventIds is empty");
			if(eventIds.Count == 1)
				return CreatePrepareLogRecord(stream, expectedVersion, eventTypes[0], eventIds[0], transactionPosition);

			var numEvents = eventTypes.Count;

			var prepares = new List<PrepareLogRecord>();
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

		public void WriteToDB(IList<PrepareLogRecord> prepares){
			foreach(var prepare in prepares){
				((FakeInMemoryTfReader)_tfReader).AddRecord(prepare, prepare.LogPosition);
			}
		}

		public void WriteToDB(CommitLogRecord commit){
			((FakeInMemoryTfReader)_tfReader).AddRecord(commit, commit.LogPosition);
		}

		public void PreCommitToIndex(IList<PrepareLogRecord> prepares){
			_indexWriter.PreCommit(prepares);
		}

		public void PreCommitToIndex(CommitLogRecord commitLogRecord){
			_indexWriter.PreCommit(commitLogRecord);
		}

		public void CommitToIndex(IList<PrepareLogRecord> prepares){
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
			_tableIndex = new FakeInMemoryTableIndex();
			_readerPool = new ObjectPool<ITransactionFileReader>(
				"ReadIndex readers pool", 5, 100,
				() => _tfReader);
			_indexBackend = new IndexBackend(_readerPool, 100000, 100000);
			_indexReader = new IndexReader(_indexBackend, _tableIndex, new StreamMetadata(maxCount: 100000), 100, false);
			_indexWriter = new IndexWriter(_indexBackend, _indexReader);
			_indexCommitter = new IndexCommitter(_publisher, _indexBackend, _indexReader, _tableIndex, false);

			WriteEvents();
		}

		[OneTimeTearDown]
		public virtual void TestFixtureTearDown() {
			_readerPool.Dispose();
		}
	}
}
