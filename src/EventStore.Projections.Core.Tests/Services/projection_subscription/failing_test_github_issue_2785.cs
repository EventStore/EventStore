// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription;

public class
	failing_test_github_issue_2785 :
		TestFixtureWithProjectionSubscription {

	protected override void Given() {
		_source = source => {
			source.FromAll();
			source.IncludeEvent("good-event-type");
		};
	}

	protected override void When() {
	}

	[Test]
	public void should_gracefully_handle_resolved_linkto_events() {
		var stream = "any-stream-name";
		var eventType = "any-event-type";
		var position = new TFPos(-1, 200); //resolved linkTo event with incomplete TF position
		var resolvedLinkToEvent = true;
		_subscription.Handle(
			ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
				Guid.NewGuid(), position, stream, 1, resolvedLinkToEvent, Guid.NewGuid(),
				eventType, false, new byte[0], new byte[0]));
	}
}
