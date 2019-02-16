using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.Services.Replication;
using NUnit.Framework;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Services.Storage;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	public abstract class with_index_committer_service {
		protected const int _timeoutSeconds = 5;
		protected string _eventStreamId = "test_stream";
		protected int _commitCount = 2;
		protected long _replicationPosition = 0;
		protected ITableIndex _tableIndex;

		protected ICheckpoint _replicationCheckpoint;
		protected ICheckpoint _writerCheckpoint;
		protected InMemoryBus _publisher = new InMemoryBus("publisher");
		protected List<StorageMessage.CommitReplicated> _handledMessages = new List<StorageMessage.CommitReplicated>();

		protected IndexCommitterService _service;
		protected FakeIndexCommitter _indexCommitter;
		protected ITFChunkScavengerLogManager _tfChunkScavengerLogManager;

		protected int _expectedCommitReplicatedMessages;

		[OneTimeSetUp]
		public virtual void TestFixtureSetUp() {
			_indexCommitter = new FakeIndexCommitter();
			_replicationCheckpoint = new InMemoryCheckpoint(_replicationPosition);
			_writerCheckpoint = new InMemoryCheckpoint(0);
			_publisher.Subscribe(new AdHocHandler<StorageMessage.CommitReplicated>(m => _handledMessages.Add(m)));
			_tableIndex = new FakeTableIndex();
			_tfChunkScavengerLogManager = new FakeTfChunkLogManager();
			_service = new IndexCommitterService(_indexCommitter, _publisher, _replicationCheckpoint, _writerCheckpoint,
				_commitCount, _tableIndex);
			_service.Init(0);
			When();
		}

		[OneTimeTearDown]
		public virtual void TestFixtureTearDown() {
			_service.Stop();
		}

		public abstract void When();

		protected void AddPendingPrepare(long transactionPosition, long postPosition = -1) {
			postPosition = postPosition == -1 ? transactionPosition : postPosition;
			var prepare = CreatePrepare(transactionPosition, transactionPosition);
			_service.AddPendingPrepare(new PrepareLogRecord[] {prepare}, postPosition);
		}

		protected void AddPendingPrepares(long transactionPosition, long[] logPositions) {
			var prepares = new List<PrepareLogRecord>();
			foreach (var pos in logPositions) {
				prepares.Add(CreatePrepare(transactionPosition, pos));
			}

			_service.AddPendingPrepare(prepares.ToArray(), logPositions[logPositions.Length - 1]);
		}

		private PrepareLogRecord CreatePrepare(long transactionPosition, long logPosition) {
			return LogRecord.Prepare(logPosition, Guid.NewGuid(), Guid.NewGuid(), transactionPosition, 0,
				_eventStreamId, -1, PrepareFlags.None, "testEvent",
				new byte[10], new byte[0]);
		}


		protected void AddPendingCommit(long transactionPosition, long logPosition, long postPosition = -1) {
			postPosition = postPosition == -1 ? logPosition : postPosition;
			var commit = LogRecord.Commit(logPosition, Guid.NewGuid(), transactionPosition, 0);
			_service.AddPendingCommit(commit, postPosition);
		}

		protected void BecomeMaster() {
			_service.Handle(new SystemMessage.BecomeMaster(Guid.NewGuid()));
		}

		protected void BecomeUnknown() {
			_service.Handle(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
		}

		protected void BecomeSlave() {
			var masterIPEndPoint = new IPEndPoint(IPAddress.Loopback, 2113);
			_service.Handle(new SystemMessage.BecomeSlave(Guid.NewGuid(), new VNodeInfo(Guid.NewGuid(), 1,
				masterIPEndPoint, masterIPEndPoint, masterIPEndPoint,
				masterIPEndPoint, masterIPEndPoint, masterIPEndPoint)));
		}
	}

	public class FakeIndexCommitter : IIndexCommitter {
		public List<PrepareLogRecord> CommittedPrepares = new List<PrepareLogRecord>();
		public List<CommitLogRecord> CommittedCommits = new List<CommitLogRecord>();

		public long LastCommitPosition { get; set; }

		public void Init(long buildToPosition) {
		}

		public void Dispose() {
		}

		public long Commit(CommitLogRecord commit, bool isTfEof, bool cacheLastEventNumber) {
			CommittedCommits.Add(commit);
			return 0;
		}

		public long Commit(IList<PrepareLogRecord> committedPrepares, bool isTfEof, bool cacheLastEventNumber) {
			CommittedPrepares.AddRange(committedPrepares);
			return 0;
		}

		public long GetCommitLastEventNumber(CommitLogRecord commit) {
			return 0;
		}
	}
}
