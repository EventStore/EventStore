namespace EventStore.Core.Tests.ClientAPI.Helpers;

public record CatchUpSubscriptionSettings(
	int MaxLiveQueueSize,
	int ReadBatchSize,
	bool VerboseLogging,
	bool ResolveLinkTos,
	string SubscriptionName = "");
