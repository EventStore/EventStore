using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Index;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Services.Replication;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.IndexCommitter {
	public abstract class with_index_committer_service<TLogFormat, TStreamId> {
		protected int CommitCount = 2;
		protected ITableIndex TableIndex;

		protected ICheckpoint ReplicationCheckpoint;
		protected ICheckpoint WriterCheckpoint;
		protected InMemoryBus Publisher = new InMemoryBus("publisher");
		protected ConcurrentQueue<StorageMessage.CommitIndexed> CommitReplicatedMgs = new ConcurrentQueue<StorageMessage.CommitIndexed>();
		protected ConcurrentQueue<ReplicationTrackingMessage.IndexedTo> IndexWrittenMgs = new ConcurrentQueue<ReplicationTrackingMessage.IndexedTo>();

		protected IndexCommitterService<TStreamId> Service;
		protected FakeIndexCommitter<TStreamId> IndexCommitter;
		protected ITFChunkScavengerLogManager TfChunkScavengerLogManager;
		
		[OneTimeSetUp]
		public virtual void TestFixtureSetUp() {
			IndexCommitter = new FakeIndexCommitter<TStreamId>();
			ReplicationCheckpoint = new InMemoryCheckpoint();
			WriterCheckpoint = new InMemoryCheckpoint(0);
			TableIndex = new FakeTableIndex<TStreamId>();
			TfChunkScavengerLogManager = new FakeTfChunkLogManager();
			Service = new IndexCommitterService<TStreamId>(IndexCommitter, Publisher, WriterCheckpoint, ReplicationCheckpoint, CommitCount, TableIndex, new QueueStatsManager());
			Service.Init(0);
			Publisher.Subscribe(new AdHocHandler<StorageMessage.CommitIndexed>(m => CommitReplicatedMgs.Enqueue(m)));
			Publisher.Subscribe(new AdHocHandler<ReplicationTrackingMessage.IndexedTo>(m => IndexWrittenMgs.Enqueue(m)));
			Publisher.Subscribe<ReplicationTrackingMessage.ReplicatedTo>(Service);
			Given();

			When();
		}

		[OneTimeTearDown]
		public virtual void TestFixtureTearDown() {
			Service.Stop();
		}
		public abstract void Given();
		public abstract void When();

		protected void AddPendingPrepare(long transactionPosition, long postPosition = -1) {
			postPosition = postPosition == -1 ? transactionPosition : postPosition;
			var prepare = CreatePrepare(transactionPosition, transactionPosition);
			Service.AddPendingPrepare(new[] { prepare }, postPosition);
		}

		protected void AddPendingPrepares(long transactionPosition, long[] logPositions) {
			var prepares = new List<IPrepareLogRecord<TStreamId>>();
			foreach (var pos in logPositions) {
				prepares.Add(CreatePrepare(transactionPosition, pos));
			}

			Service.AddPendingPrepare(prepares.ToArray(), logPositions[^1]);
		}

		private IPrepareLogRecord<TStreamId> CreatePrepare(long transactionPosition, long logPosition) {
			var recordFactory = LogFormatHelper<TLogFormat, TStreamId>.RecordFactory;
			var streamId = LogFormatHelper<TLogFormat, TStreamId>.StreamId;
			return LogRecord.Prepare(recordFactory, logPosition, Guid.NewGuid(), Guid.NewGuid(), transactionPosition, 0,
				streamId, -1, PrepareFlags.None, "testEvent",
				new byte[10], new byte[0]);
		}


		protected void AddPendingCommit(long transactionPosition, long logPosition, long postPosition = -1) {
			postPosition = postPosition == -1 ? logPosition : postPosition;
			var commit = LogRecord.Commit(logPosition, Guid.NewGuid(), transactionPosition, 0);
			Service.AddPendingCommit(commit, postPosition);
		}
	}

	public class FakeIndexCommitter<TStreamId> : IIndexCommitter<TStreamId> {
		public ConcurrentQueue<IPrepareLogRecord<TStreamId>> CommittedPrepares = new ConcurrentQueue<IPrepareLogRecord<TStreamId>>();
		public ConcurrentQueue<CommitLogRecord> CommittedCommits = new ConcurrentQueue<CommitLogRecord>();

		public long LastIndexedPosition { get; set; }

		public void Init(long buildToPosition) {
		}

		public void Dispose() {
		}

		public long Commit(CommitLogRecord commit, bool isTfEof, bool cacheLastEventNumber) {
			CommittedCommits.Enqueue(commit);
			return 0;
		}

		public long Commit(IList<IPrepareLogRecord<TStreamId>> committedPrepares, bool isTfEof, bool cacheLastEventNumber) {
			foreach (var prepare in committedPrepares) {
				CommittedPrepares.Enqueue(prepare);	
			}
			return 0;
		}

		public long GetCommitLastEventNumber(CommitLogRecord commit) {
			return 0;
		}
	}
}
