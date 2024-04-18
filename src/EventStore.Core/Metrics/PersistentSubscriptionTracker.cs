using System.Collections.Generic;
using EventStore.Core.Messages;

namespace EventStore.Core.Metrics;

public interface IPersistentSubscriptionTracker {
	public void Register(List<MonitoringMessage.PersistentSubscriptionInfo> statsList);
}

public class PersistentSubscriptionTracker : IPersistentSubscriptionTracker {

	private readonly PersistentSubscriptionMetric _metrics;

	public PersistentSubscriptionTracker(PersistentSubscriptionMetric metrics) {
		_metrics = metrics;
	}

	public void Register(List<MonitoringMessage.PersistentSubscriptionInfo> statsList) {
		_metrics.Register(statsList);
	}

	public class NoOp : IPersistentSubscriptionTracker {
		public void Register(List<MonitoringMessage.PersistentSubscriptionInfo> statsList) { }
	}
}
