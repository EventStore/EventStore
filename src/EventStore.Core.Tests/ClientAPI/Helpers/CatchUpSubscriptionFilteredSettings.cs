namespace EventStore.Core.Tests.ClientAPI.Helpers;

public class CatchUpSubscriptionFilteredSettings {
	public static readonly CatchUpSubscriptionFilteredSettings Default =
		new CatchUpSubscriptionFilteredSettings(10_000, 500);

	public static CatchUpSubscriptionFilteredSettings FromSettings(CatchUpSubscriptionSettings setts) {
		return new CatchUpSubscriptionFilteredSettings(setts.MaxLiveQueueSize, setts.ReadBatchSize, setts.VerboseLogging, setts.ResolveLinkTos);
	}
	public int MaxLiveQueueSize { get; init; }
	public int ReadBatchSize { get; init; }
	public bool ResolveLinkTos { get; init; }
	public int MaxSearchWindow { get; init; }

	public CatchUpSubscriptionFilteredSettings(
		int maxLiveQueueSize,
		int readBatchSize,
		bool verboseLogging = false,
		bool resolveLinkTos = false,
		int maxSearchWindow = 500,
		string subscriptionName = "") {
		MaxLiveQueueSize = maxLiveQueueSize;
		ReadBatchSize = readBatchSize;
		ResolveLinkTos = resolveLinkTos;
		MaxSearchWindow = maxSearchWindow;
	}
}
