using System;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLogV2.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.IndexCommitter {
	[TestFixture]
	public class
		when_index_committer_service_receives_replicated_lower_position : with_index_committer_service {
		private readonly Guid _correlationId = Guid.NewGuid();
		private readonly long _logPosition = 2000;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			ReplicationCheckpoint = new InMemoryCheckpoint(100);
			base.TestFixtureSetUp();
		}
		public override void Given() { }
		public override void When() {
			Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
			ReplicationCheckpoint.Write(_logPosition - 1);
			ReplicationCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.ReplicatedTo(_logPosition - 1));
		}
		
		[Test]
		public void commit_replicated_message_should_not_have_been_published() {
			Assert.AreEqual(0, CommitReplicatedMgs.Count);
		}
		[Test]
		public void index_written_message_should_have_not_been_published() {
			Assert.AreEqual(0, IndexWrittenMgs.Count);
		}
		[Test]
		public void index_written_message_should_not_have_been_published() {
			Assert.AreEqual(0, IndexWrittenMgs.Count);
		}
		[Test]
		public void index_should_not_have_been_updated() {
			Assert.AreEqual(0, IndexCommitter.CommittedPrepares.Count);
		}
	}
}
