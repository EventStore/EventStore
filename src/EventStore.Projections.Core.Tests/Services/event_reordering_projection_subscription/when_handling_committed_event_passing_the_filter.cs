﻿using System;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reordering_projection_subscription {
	[TestFixture]
	public class
		when_handling_committed_event_passing_the_filter : TestFixtureWithEventReorderingProjectionSubscription {
		protected override void When() {
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), new TFPos(200, 150), "a", 1, false, Guid.NewGuid(), "bad-event-type", false,
					new byte[0], new byte[0]));
		}

		[Test]
		public void event_is_not_passed_to_downstream_handler_immediately() {
			Assert.AreEqual(0, _eventHandler.HandledMessages.Count);
		}
	}
}
