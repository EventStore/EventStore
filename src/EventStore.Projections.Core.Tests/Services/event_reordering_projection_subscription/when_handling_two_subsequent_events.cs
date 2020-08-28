﻿using System;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reordering_projection_subscription {
	[TestFixture]
	public class when_handling_two_subsequent_events : TestFixtureWithEventReorderingProjectionSubscription {
		private Guid _firstEventId;
		private DateTime _firstEventTimestamp;
#pragma warning disable 108,114
		private int _timeBetweenEvents;
#pragma warning restore 108,114

		protected override void When() {
			_firstEventId = Guid.NewGuid();
			_firstEventTimestamp = DateTime.UtcNow;
			_timeBetweenEvents = 1;

			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), new TFPos(200, 150), "a", 1, false, _firstEventId, "bad-event-type", false,
					new byte[0], new byte[0], _firstEventTimestamp));
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), new TFPos(300, 250), "a", 2, false, Guid.NewGuid(), "bad-event-type", false,
					new byte[0], new byte[0], _firstEventTimestamp.AddMilliseconds(_timeBetweenEvents)));
		}

		[Test]
		public void no_events_are_passed_to_downstream_handler_immediately() {
			Assert.AreEqual(0, _eventHandler.HandledMessages.Count);
		}
	}
}
