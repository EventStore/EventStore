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
using EventStore.Core.Services.Commit;
using System.Linq;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	public abstract class with_index_committer_service {
		protected const int TimeoutSeconds = 5;
		protected string EventStreamId = "test_stream";
		protected int CommitCount = 2;
		protected long ReplicationPosition = 0;
		protected ITableIndex TableIndex;
		protected readonly Guid ReplicaId = Guid.NewGuid();

		protected ICheckpoint ReplicationCheckpoint;
		protected ICheckpoint WriterCheckpoint;
		protected InMemoryBus Publisher = new InMemoryBus("publisher");
		protected List<StorageMessage.CommitIndexed> CommitReplicatedMgs = new List<StorageMessage.CommitIndexed>();
		protected List<CommitMessage.IndexedTo> IndexWrittenMgs = new List<CommitMessage.IndexedTo>();

		protected IndexCommitterService Service;
		protected FakeIndexCommitter IndexCommitter;
		protected ITFChunkScavengerLogManager TfChunkScavengerLogManager;
		protected CommitTrackerService CommitTracker;

		protected int _expectedCommitReplicatedMessages;

		[OneTimeSetUp]
		public virtual void TestFixtureSetUp() {
			IndexCommitter = new FakeIndexCommitter();
			ReplicationCheckpoint = new InMemoryCheckpoint(ReplicationPosition);
			WriterCheckpoint = new InMemoryCheckpoint(0);
			Publisher.Subscribe(new AdHocHandler<StorageMessage.CommitIndexed>(m => CommitReplicatedMgs.Add(m)));
			Publisher.Subscribe(new AdHocHandler<CommitMessage.IndexedTo>(m => IndexWrittenMgs.Add(m)));
			TableIndex = new FakeTableIndex();
			TfChunkScavengerLogManager = new FakeTfChunkLogManager();
			CommitTracker = new CommitTrackerService(Publisher, CommitLevel.MasterIndexed, 3, ReplicationCheckpoint, WriterCheckpoint);
			CommitTracker.Start();
			Service = new IndexCommitterService(IndexCommitter, Publisher, WriterCheckpoint, ReplicationCheckpoint, CommitCount, TableIndex, new QueueStatsManager());
			Service.Init(0);
			Publisher.Subscribe<CommitMessage.ReplicatedTo>(Service);
			Publisher.Subscribe<CommitMessage.MasterReplicatedTo>(CommitTracker);
			
			BecomeMaster();
			Given();

			When();
		}

		[OneTimeTearDown]
		public virtual void TestFixtureTearDown() {
			Service.Stop();
		}
		public abstract void Given();
		public abstract void When();

		protected void AddPendingPrepare(long transactionPosition, long postPosition = -1, bool publishChaserMsgs = true) {
			postPosition = postPosition == -1 ? transactionPosition : postPosition;
			var prepare = CreatePrepare(transactionPosition, transactionPosition);
			Service.AddPendingPrepare(new PrepareLogRecord[] { prepare }, postPosition);
			if (publishChaserMsgs) {
				CommitTracker.Handle(new CommitMessage.WrittenTo(postPosition));
				CommitTracker.Handle(new CommitMessage.ReplicaWrittenTo(postPosition, ReplicaId));
			}
		}

		protected void AddPendingPrepares(long transactionPosition, long[] logPositions, bool publishChaserMsgs = true) {
			var prepares = new List<PrepareLogRecord>();
			foreach (var pos in logPositions) {
				prepares.Add(CreatePrepare(transactionPosition, pos));
			}

			Service.AddPendingPrepare(prepares.ToArray(), logPositions[logPositions.Length - 1]);
			if (publishChaserMsgs) {
				CommitTracker.Handle(new CommitMessage.WrittenTo(logPositions.Max()));
				CommitTracker.Handle(new CommitMessage.ReplicaWrittenTo(logPositions.Max(), ReplicaId));
			}
		}

		private PrepareLogRecord CreatePrepare(long transactionPosition, long logPosition) {
			return LogRecord.Prepare(logPosition, Guid.NewGuid(), Guid.NewGuid(), transactionPosition, 0,
				EventStreamId, -1, PrepareFlags.None, "testEvent",
				new byte[10], new byte[0]);
		}


		protected void AddPendingCommit(long transactionPosition, long logPosition, long postPosition = -1, bool publishChaserMsgs = true) {
			postPosition = postPosition == -1 ? logPosition : postPosition;
			var commit = LogRecord.Commit(logPosition, Guid.NewGuid(), transactionPosition, 0);
			Service.AddPendingCommit(commit, postPosition);
			if (publishChaserMsgs) {
				CommitTracker.Handle(new CommitMessage.WrittenTo(postPosition));
				CommitTracker.Handle(new CommitMessage.ReplicaWrittenTo(postPosition, ReplicaId));
			}
		}

		protected void BecomeMaster() {
			CommitTracker.Handle(new SystemMessage.BecomeMaster(Guid.NewGuid()));
		}

		protected void BecomeUnknown() {
			CommitTracker.Handle(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
		}

		protected void BecomeSlave() {
			var masterIPEndPoint = new IPEndPoint(IPAddress.Loopback, 2113);
			var msg = new SystemMessage.BecomeSlave(Guid.NewGuid(), new VNodeInfo(Guid.NewGuid(), 1,
				masterIPEndPoint, masterIPEndPoint, masterIPEndPoint,
				masterIPEndPoint, masterIPEndPoint, masterIPEndPoint, false));
			CommitTracker.Handle(msg);
		}
	}

	public class FakeIndexCommitter : IIndexCommitter {
		public List<PrepareLogRecord> CommittedPrepares = new List<PrepareLogRecord>();
		public List<CommitLogRecord> CommittedCommits = new List<CommitLogRecord>();

		public long LastIndexedPosition { get; set; }

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
