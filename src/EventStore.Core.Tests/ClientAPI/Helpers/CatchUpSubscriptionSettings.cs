namespace EventStore.Core.Tests.ClientAPI.Helpers;

public record CatchUpSubscriptionSettings(
	int MaxLiveQueueSize,
	int ReadBatchSize,
	bool VerboseLogging,
	bool ResolveLinkTos,
	string SubscriptionName = "") {

	public static readonly CatchUpSubscriptionSettings Default =
		new CatchUpSubscriptionSettings(10_000, 500, false, true, string.Empty);
}
