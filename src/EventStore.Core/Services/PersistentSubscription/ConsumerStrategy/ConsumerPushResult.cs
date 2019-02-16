namespace EventStore.Core.Services.PersistentSubscription.ConsumerStrategy {
	public enum ConsumerPushResult {
		Sent,
		Skipped,
		NoMoreCapacity
	}
}
