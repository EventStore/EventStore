using System;

namespace EventStore.ClientAPI
{
	public class CatchUpSubscriptionFilteredSettings : CatchUpSubscriptionSettings {
		public CatchUpSubscriptionFilteredSettings(int maxLiveQueueSize, int readBatchSize, bool verboseLogging,
			bool resolveLinkTos, int maxSearchWindow, string subscriptionName = "") : base(maxLiveQueueSize, readBatchSize, verboseLogging,
			resolveLinkTos, subscriptionName) {
			MaxSearchWindow = maxSearchWindow;
		}

		public int MaxSearchWindow { get; }

		public new static readonly CatchUpSubscriptionFilteredSettings Default = new CatchUpSubscriptionFilteredSettings(
			Consts.CatchUpDefaultMaxPushQueueSize,
			Consts.CatchUpDefaultReadBatchSize,
			verboseLogging: false,
			resolveLinkTos: true,
			maxSearchWindow: Consts.CatchUpDefaultReadBatchSize,
			subscriptionName: String.Empty
		);
	}
}
