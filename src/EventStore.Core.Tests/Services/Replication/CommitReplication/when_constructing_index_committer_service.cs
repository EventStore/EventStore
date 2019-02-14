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
using EventStore.Core.Services.Storage;
using EventStore.Core.Tests.Services.Storage;

namespace EventStore.Core.Tests.Services.Replication.CommitReplication {
	[TestFixture]
	public class when_creating_index_committer_service {
		protected int _commitCount = 2;
		protected ICheckpoint _replicationCheckpoint = new InMemoryCheckpoint(0);
		protected ICheckpoint _writerCheckpoint = new InMemoryCheckpoint(0);
		protected InMemoryBus _publisher = new InMemoryBus("publisher");
		protected FakeIndexCommitter _indexCommitter = new FakeIndexCommitter();
		protected ITableIndex _tableIndex = new FakeTableIndex();

		[Test]
		public void null_index_committer_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => new IndexCommitterService(null, _publisher,
				_replicationCheckpoint, _writerCheckpoint, _commitCount, _tableIndex));
		}

		[Test]
		public void null_publisher_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => new IndexCommitterService(_indexCommitter, null,
				_replicationCheckpoint, _writerCheckpoint, _commitCount, _tableIndex));
		}

		[Test]
		public void null_writer_checkpoint_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() => new IndexCommitterService(_indexCommitter, _publisher,
				_replicationCheckpoint, null, _commitCount, _tableIndex));
		}

		[Test]
		public void null_replication_checkpoint_throws_argument_null_exception() {
			Assert.Throws<ArgumentNullException>(() =>
				new IndexCommitterService(_indexCommitter, _publisher, null, _writerCheckpoint, _commitCount,
					_tableIndex));
		}

		[Test]
		public void commit_count_of_zero_throws_argument_out_of_range_exception() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new IndexCommitterService(_indexCommitter, _publisher,
				_replicationCheckpoint, _writerCheckpoint, 0, _tableIndex));
		}

		[Test]
		public void negative_commit_count_throws_argument_out_of_range_exception() {
			Assert.Throws<ArgumentOutOfRangeException>(() => new IndexCommitterService(_indexCommitter, _publisher,
				_replicationCheckpoint, _writerCheckpoint, -1, _tableIndex));
		}
	}
}
