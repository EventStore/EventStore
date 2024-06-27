﻿using System;
using System.Collections.Generic;
using EventStore.Core.Messages;

namespace EventStore.Core.Metrics;

public interface IPersistentSubscriptionTracker {
	void OnNewStats(IReadOnlyList<MonitoringMessage.PersistentSubscriptionInfo> newStats);
}

public class PersistentSubscriptionTracker : IPersistentSubscriptionTracker {
	private readonly PersistentSubscriptionItemsProcessedMetric _persistentSubscriptionItemsProcessedMetric;
	private readonly PersistentSubscriptionConnectionCountMetric _persistentSubscriptionConnectionCountMetric;
	private readonly PersistentSubscriptionInFlightMessagesMetric _persistentSubscriptionInFlightMessagesMetric;
	private readonly PersistentSubscriptionParkedMessagesMetric _persistentSubscriptionParkedMessagesMetric;
	private readonly PersistentSubscriptionOldestParkedMessageMetric _persistentSubscriptionOldestParkedMessageMetric;
	private readonly PersistentSubscriptionLastCheckpointedEventMetric _persistentSubscriptionLastProcessedEventMetric;
	private readonly PersistentSubscriptionLastKnownEventMetric _persistentSubscriptionLastKnownEventMetric;
	private readonly PersistentSubscriptionLastCheckpointedEventCommitPositionMetric _persistentSubscriptionLastCheckpointedEventCommitPositionMetric;
	private readonly PersistentSubscriptionLastKnownEventCommitPositionMetric _persistentSubscriptionLastKnownEventCommitPositionMetric;

	private IReadOnlyList<MonitoringMessage.PersistentSubscriptionInfo> _currentStats = [];

	public PersistentSubscriptionTracker(
		PersistentSubscriptionItemsProcessedMetric persistentSubscriptionItemsProcessedMetric,
		PersistentSubscriptionConnectionCountMetric persistentSubscriptionConnectionCountMetric,
		PersistentSubscriptionInFlightMessagesMetric persistentSubscriptionInFlightMessagesMetric,
		PersistentSubscriptionParkedMessagesMetric persistentSubscriptionParkedMessagesMetric,
		PersistentSubscriptionOldestParkedMessageMetric persistentSubscriptionOldestParkedMessageMetric,
		PersistentSubscriptionLastCheckpointedEventMetric persistentSubscriptionLastProcessedEventMetric,
		PersistentSubscriptionLastKnownEventMetric persistentSubscriptionLastKnownEventMetric,
		PersistentSubscriptionLastCheckpointedEventCommitPositionMetric persistentSubscriptionLastCheckpointedEventCommitPositionMetric,
		PersistentSubscriptionLastKnownEventCommitPositionMetric persistentSubscriptionLastKnownEventCommitPositionMetric
	) {
		_persistentSubscriptionItemsProcessedMetric = persistentSubscriptionItemsProcessedMetric;
		_persistentSubscriptionConnectionCountMetric = persistentSubscriptionConnectionCountMetric;
		_persistentSubscriptionInFlightMessagesMetric = persistentSubscriptionInFlightMessagesMetric;
		_persistentSubscriptionParkedMessagesMetric = persistentSubscriptionParkedMessagesMetric;
		_persistentSubscriptionOldestParkedMessageMetric = persistentSubscriptionOldestParkedMessageMetric;
		_persistentSubscriptionLastProcessedEventMetric = persistentSubscriptionLastProcessedEventMetric;
		_persistentSubscriptionLastKnownEventMetric = persistentSubscriptionLastKnownEventMetric;
		_persistentSubscriptionLastCheckpointedEventCommitPositionMetric = persistentSubscriptionLastCheckpointedEventCommitPositionMetric;
		_persistentSubscriptionLastKnownEventCommitPositionMetric = persistentSubscriptionLastKnownEventCommitPositionMetric;

		Func<IReadOnlyList<MonitoringMessage.PersistentSubscriptionInfo>> getCurrentStats = () => _currentStats;

		_persistentSubscriptionItemsProcessedMetric.Register(getCurrentStats);
		_persistentSubscriptionConnectionCountMetric.Register(getCurrentStats);
		_persistentSubscriptionInFlightMessagesMetric.Register(getCurrentStats);
		_persistentSubscriptionParkedMessagesMetric.Register(getCurrentStats);
		_persistentSubscriptionOldestParkedMessageMetric.Register(getCurrentStats);
		_persistentSubscriptionLastProcessedEventMetric.Register(getCurrentStats);
		_persistentSubscriptionLastKnownEventMetric.Register(getCurrentStats);
		_persistentSubscriptionLastCheckpointedEventCommitPositionMetric.Register(getCurrentStats);
		_persistentSubscriptionLastKnownEventCommitPositionMetric.Register(getCurrentStats);
	}

	public void OnNewStats(IReadOnlyList<MonitoringMessage.PersistentSubscriptionInfo> newStats) {
		_currentStats = newStats ?? [];
	}

	public class NoOp : IPersistentSubscriptionTracker {
		public void OnNewStats(IReadOnlyList<MonitoringMessage.PersistentSubscriptionInfo> newStats) { }
	}
}
