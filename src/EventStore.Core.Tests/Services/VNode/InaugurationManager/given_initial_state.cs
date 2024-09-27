using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.VNode.InaugurationManagement {
	[TestFixture]
	public class given_initial_state : InaugurationManagerTests {
		protected override void Given() {
			_publisher.Messages.Clear();
		}

		[Test]
		public void when_become_pre_leader() {
			When(new SystemMessage.BecomePreLeader(_correlationId1));
			AssertWaitingForChaser(_correlationId1);
		}

		[Test]
		public void when_become_other_node_state() {
			When(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
			AssertInitial();
		}
	}
}
