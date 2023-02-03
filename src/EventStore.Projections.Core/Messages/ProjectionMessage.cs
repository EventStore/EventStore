namespace EventStore.Projections.Core.Messages {
	// The name of this enum and its members are used for metrics
	public enum ProjectionMessage {
		None,
		CoreManagement,
		CoreProcessing,
		CoreStatus,
		EventReaderSubscription,
		FeedReader,
		Management,
		Misc,
		ReaderCoreService,
		ReaderSubscription,
		ReaderSubscriptionManagement,
		ServiceMessage,
		Subsystem,
	}
}
