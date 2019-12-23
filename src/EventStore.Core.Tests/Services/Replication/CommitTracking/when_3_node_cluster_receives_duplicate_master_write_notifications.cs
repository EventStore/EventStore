using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CommitTracking {
	[TestFixture]
	public class when_3_node_cluster_receives_duplicate_master_write_notifications : with_clustered_commit_tracker_service {
		private long _logPosition = 4000;

		public override void When() {
			BecomeMaster();
			Service.Handle(new CommitMessage.LogWrittenTo(_logPosition));
			Service.Handle(new CommitMessage.LogWrittenTo(_logPosition));			
			AssertEx.IsOrBecomesTrue(() => Service.IsIdle());
		}

		[Test]
		public void log_committed_to_should_not_be_sent() {
			Assert.AreEqual(0, LogCommittedTos.Count);		
		}

		[Test]
		public void committed_to_should_not_be_sent() {
			Assert.AreEqual(0, CommittedTos.Count);
		}
	}
}
