using System;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public class CatchUpSubscriptionFilteredSettings {
	public static readonly CatchUpSubscriptionFilteredSettings Default =
		new CatchUpSubscriptionFilteredSettings(10_000, 500);

	public CatchUpSubscriptionFilteredSettings(
		int maxLiveQueueSize,
		int readBatchSize,
		bool verboseLogging = false,
		bool resolveLinkTos = false,
		int maxSearchWindow = 500,
		string subscriptionName = "") {

	}
}
