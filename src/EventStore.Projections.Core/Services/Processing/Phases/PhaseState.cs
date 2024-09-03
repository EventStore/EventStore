namespace EventStore.Projections.Core.Services.Processing.Phases;

public enum PhaseState {
	Unknown,
	Stopped,
	Starting,
	Running,
}

public enum PhaseSubscriptionState {
	Unknown = 0,
	Unsubscribed,
	Subscribing,
	Subscribed,
	Failed
}
