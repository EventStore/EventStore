using System;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.IndexCommitter {
	[TestFixture]
	public class when_index_committer_service_receives_replicated_to : with_index_committer_service {
		private readonly Guid _correlationId = Guid.NewGuid();
		private readonly long _logPosition = 4000;

		public override void TestFixtureSetUp() {
			ReplicationCheckpoint = new InMemoryCheckpoint(_logPosition);
			base.TestFixtureSetUp();
		}
		public override void Given() { }
		public override void When() {
			
			AddPendingPrepare(_logPosition);
			var commitIndex = Publisher.WaitForNext<StorageMessage.CommitIndexed>();
			Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
			ReplicationCheckpoint.Write(_logPosition);
			ReplicationCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.ReplicatedTo(_logPosition));
			
			if (!commitIndex.Wait(TimeSpan.FromSeconds(TimeoutSeconds))) {
				Assert.Fail("Timed out waiting for commit replicated messages to be published");
			}
		}
		
		[Test]
		public void commit_replicated_message_should_have_been_published() {
			Assert.AreEqual(1, CommitReplicatedMgs.Count);
			Assert.AreEqual(_logPosition, CommitReplicatedMgs[0].TransactionPosition);
		}
		[Test]
		public void index_written_message_should_have_been_published() {
			Assert.AreEqual(1, IndexWrittenMgs.Count);
			Assert.AreEqual(_logPosition, IndexWrittenMgs[0].LogPosition);
		}
		[Test]
		public void index_should_have_been_updated() {
			Assert.AreEqual(1, IndexCommitter.CommittedPrepares.Count);
			Assert.AreEqual(_logPosition, IndexCommitter.CommittedPrepares[0].LogPosition);
		}
	}
}
