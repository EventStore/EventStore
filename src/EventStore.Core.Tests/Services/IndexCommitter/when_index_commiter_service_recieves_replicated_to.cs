using System;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.IndexCommitter {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_index_committer_service_receives_replicated_to<TLogFormat, TStreamId> : with_index_committer_service<TLogFormat, TStreamId> {
		private readonly Guid _correlationId = Guid.NewGuid();
		private readonly long _logPosition = 4000;

		public override void TestFixtureSetUp() {
			ReplicationCheckpoint = new InMemoryCheckpoint(_logPosition);
			base.TestFixtureSetUp();
		}
		public override void Given() { }
		public override void When() {

			AddPendingPrepare(_logPosition);
			Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPosition, _logPosition, 0, 0));
			ReplicationCheckpoint.Write(_logPosition);
			ReplicationCheckpoint.Flush();
			Service.Handle(new ReplicationTrackingMessage.ReplicatedTo(_logPosition));
		}

		[Test]
		public void commit_replicated_message_should_have_been_published() {
			AssertEx.IsOrBecomesTrue(() => 1 == CommitReplicatedMgs.Count);
			Assert.True(CommitReplicatedMgs.TryDequeue(out var msg));
			Assert.AreEqual(_logPosition, msg.TransactionPosition);
		}
		[Test]
		public void index_written_message_should_have_been_published() {
			AssertEx.IsOrBecomesTrue(() => 1 == IndexWrittenMgs.Count);
			Assert.True(IndexWrittenMgs.TryDequeue(out var msg));
			Assert.AreEqual(_logPosition, msg.LogPosition);
		}
		[Test]
		public void index_should_have_been_updated() {
			AssertEx.IsOrBecomesTrue(() => 1 == IndexCommitter.CommittedPrepares.Count);
			Assert.True(IndexCommitter.CommittedPrepares.TryDequeue(out var msg));
			Assert.AreEqual(_logPosition, msg.LogPosition);
		}
	}
}
