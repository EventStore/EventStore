﻿using System;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription {
	[TestFixture]
	public class when_handling_committed_event_passing_the_filter : TestFixtureWithProjectionSubscription {
		protected override void When() {
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), new TFPos(200, 150), "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
		}

		[Test]
		public void event_is_passed_to_downstream_handler() {
			Assert.AreEqual(1, _eventHandler.HandledMessages.Count);
		}
	}
}
