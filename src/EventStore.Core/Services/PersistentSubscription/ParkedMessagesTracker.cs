using System;
using EventStore.Core.Metrics;

namespace EventStore.Core.Services.PersistentSubscription;

public enum ParkReason {
	MaxRetries,
	ByClient
}

public interface IParkedMessagesTracker {
	void OnMessageParked(ParkReason parkReason);
	void OnParkedMessagesReplayed();
}

public class ParkedMessagesTracker(ParkedMessagesMetrics metrics) : IParkedMessagesTracker {
	public void OnMessageParked(ParkReason parkReason) {
		switch (parkReason) {
			case ParkReason.MaxRetries:
				metrics.OnParkedMessageDueToMaxRetries();
				break;
			case ParkReason.ByClient:
				metrics.OnParkedMessageByClient();
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(parkReason), parkReason, null);
		}
	}

	public void OnParkedMessagesReplayed() {
		metrics.OnParkedMessagesReplayed();
	}

	public class NoOp : IParkedMessagesTracker {
		public void OnMessageParked(ParkReason parkReason) { }
		public void OnParkedMessagesReplayed() { }
	}
}
