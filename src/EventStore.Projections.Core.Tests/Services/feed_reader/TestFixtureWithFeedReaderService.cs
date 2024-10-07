// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.EventReaders.Feeds;
using EventStore.Projections.Core.Tests.Services.event_reader;

namespace EventStore.Projections.Core.Tests.Services.feed_reader;

public abstract class TestFixtureWithFeedReaderService<TLogFormat, TStreamId> : TestFixtureWithEventReaderService<TLogFormat, TStreamId> {
	protected FeedReaderService _feedReaderService;

	protected override void Given1() {
		base.Given1();
		EnableReadAll();
	}

	protected override void GivenAdditionalServices() {
		_feedReaderService = new FeedReaderService(_subscriptionDispatcher, _timeProvider);
		_bus.Subscribe(_feedReaderService);
	}
}
