// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription {
	[TestFixture]
	public class when_handling_duplicate_events : TestFixtureWithProjectionSubscription {
		protected override void When() {
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), new TFPos(200, 150), "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), new TFPos(100, 50), "test-stream", 0, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
			_subscription.Handle(
				ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
					Guid.NewGuid(), new TFPos(200, 150), "test-stream", 1, false, Guid.NewGuid(),
					"bad-event-type", false, new byte[0], new byte[0]));
		}

		[Test]
		public void duplicates_are_not_passed_to_downstream_handler() {
			Assert.AreEqual(1, _eventHandler.HandledMessages.Count);
		}
	}
}
