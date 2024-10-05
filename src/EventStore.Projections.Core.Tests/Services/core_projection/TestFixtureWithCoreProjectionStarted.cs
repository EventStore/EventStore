// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Tests.Services.core_projection;

public abstract class TestFixtureWithCoreProjectionStarted<TLogFormat, TStreamId> : TestFixtureWithCoreProjection<TLogFormat, TStreamId> {
	protected Guid _subscriptionId;

	protected override void PreWhen() {
		_coreProjection.Start();
		var lastSubscribe =
			_consumer.HandledMessages.OfType<ReaderSubscriptionManagement.Subscribe>().LastOrDefault();
		_subscriptionId = lastSubscribe != null ? lastSubscribe.SubscriptionId : Guid.NewGuid();
		_bus.Publish(new EventReaderSubscriptionMessage.ReaderAssignedReader(_subscriptionId, Guid.NewGuid()));
	}
}
