using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_3_node_cluster_receives_index_overrun_write_notification : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;

		public override void When() {
			BecomeMaster();
			Service.Handle(new CommitMessage.LogWrittenTo(_logPosition));
			Service.Handle(new CommitMessage.ReplicaLogWrittenTo(_logPosition, Guid.NewGuid()));
			Service.Handle(new CommitMessage.IndexWrittenTo(_logPosition + 100));
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void log_committed_to_should_be_sent() {
			Assert.AreEqual(1, LogCommittedTos.Count);
			var commit = LogCommittedTos[0];
			Assert.NotNull(commit);
			Assert.AreEqual(_logPosition, commit.LogPosition);
		}

		[Test]
		public void committed_to_should_not_be_sent() {
			Assert.AreEqual(1, CommittedTos.Count);
			var commit = CommittedTos[0];
			Assert.NotNull(commit);
			Assert.AreEqual(_logPosition, commit.LogPosition);
		}
	}
}
