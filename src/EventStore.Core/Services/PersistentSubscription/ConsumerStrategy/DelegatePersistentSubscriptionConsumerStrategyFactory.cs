// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Bus;

namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;

class DelegatePersistentSubscriptionConsumerStrategyFactory : IPersistentSubscriptionConsumerStrategyFactory {
	public string StrategyName { get; private set; }

	private readonly Func<string, IPublisher, ISubscriber, IPersistentSubscriptionConsumerStrategy> _factory;

	public DelegatePersistentSubscriptionConsumerStrategyFactory(string strategyName,
		Func<string, IPublisher, ISubscriber, IPersistentSubscriptionConsumerStrategy> factory) {
		_factory = factory;
		StrategyName = strategyName;
	}

	public IPersistentSubscriptionConsumerStrategy Create(string subscriptionId, IPublisher mainQueue,
		ISubscriber mainBus) {
		return _factory(subscriptionId, mainQueue, mainBus);
	}
}
