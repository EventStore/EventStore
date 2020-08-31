using System;
using EventStore.Core.Bus;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLogV2.Checkpoint;
using NUnit.Framework;
// ReSharper disable ObjectCreationAsStatement

namespace EventStore.Core.Tests.Services.IndexCommitter {
	[TestFixture]
	public class when_creating_index_committer_service {
		protected int CommitCount = 2;
		protected ICheckpoint ReplicationCheckpoint = new InMemoryCheckpoint(0);
		protected ICheckpoint WriterCheckpoint = new InMemoryCheckpoint(0);
		protected InMemoryBus Publisher = new InMemoryBus("publisher");
		protected FakeIndexCommitter IndexCommitter = new FakeIndexCommitter();
		protected ITableIndex TableIndex = new FakeTableIndex();
		private readonly QueueStatsManager _queueStatsManager = new QueueStatsManager();

		[Test]
		public void null_index_committer_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => new IndexCommitterService(null, Publisher,
				 WriterCheckpoint, ReplicationCheckpoint, CommitCount, TableIndex, _queueStatsManager));
		}

		[Test]
		public void null_publisher_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => new IndexCommitterService(IndexCommitter, null,
				 WriterCheckpoint, ReplicationCheckpoint, CommitCount, TableIndex, _queueStatsManager));
		}

		[Test]
		public void null_writer_checkpoint_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => new IndexCommitterService(IndexCommitter, Publisher,
				 null, ReplicationCheckpoint, CommitCount, TableIndex, _queueStatsManager));
		}
		[Test]
		public void null_replication_checkpoint_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => new IndexCommitterService(IndexCommitter, Publisher,
				 WriterCheckpoint, null, CommitCount, TableIndex, _queueStatsManager));
		}
		[Test]
		public void commit_count_of_zero_throws_argument_out_of_range_exception() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new IndexCommitterService(IndexCommitter, Publisher,
				 WriterCheckpoint, ReplicationCheckpoint, 0, TableIndex, _queueStatsManager));
		}

		[Test]
		public void negative_commit_count_throws_argument_out_of_range_exception() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new IndexCommitterService(IndexCommitter, Publisher,
				 WriterCheckpoint, ReplicationCheckpoint, -1, TableIndex, _queueStatsManager));
		}
	}
}
