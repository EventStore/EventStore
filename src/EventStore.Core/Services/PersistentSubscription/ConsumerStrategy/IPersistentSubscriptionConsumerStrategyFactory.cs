// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy {
	public interface IPersistentSubscriptionConsumerStrategyFactory {
		string StrategyName { get; }

		IPersistentSubscriptionConsumerStrategy
			Create(string subscriptionId, IPublisher mainQueue, ISubscriber mainBus);
	}
}
