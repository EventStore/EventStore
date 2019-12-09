using System;

namespace EventStore.ClientAPI {
	/// <inheritdoc />
	public class CatchUpSubscriptionFilteredSettings : CatchUpSubscriptionSettings {
		/// <summary>
		/// Constructs a new <see cref="CatchUpSubscriptionFilteredSettings"/>
		/// </summary>
		/// <param name="maxLiveQueueSize"></param>
		/// <param name="readBatchSize"></param>
		/// <param name="verboseLogging"></param>
		/// <param name="resolveLinkTos"></param>
		/// <param name="maxSearchWindow"></param>
		/// <param name="subscriptionName"></param>
		public CatchUpSubscriptionFilteredSettings(int maxLiveQueueSize, int readBatchSize, bool verboseLogging,
			bool resolveLinkTos, int maxSearchWindow, string subscriptionName = "") : base(maxLiveQueueSize,
			readBatchSize, verboseLogging,
			resolveLinkTos, subscriptionName) {
			MaxSearchWindow = maxSearchWindow;
		}

		/// <summary>
		/// The maximum number of events the server will search through before returning the <see cref="AllEventsSlice"/>.
		/// </summary>
		public int MaxSearchWindow { get; }

		/// <summary>
		/// The default <see cref="CatchUpSubscriptionFilteredSettings"/>.
		/// </summary>
		public new static readonly CatchUpSubscriptionFilteredSettings Default =
			new CatchUpSubscriptionFilteredSettings(
				Consts.CatchUpDefaultMaxPushQueueSize,
				Consts.CatchUpDefaultReadBatchSize,
				verboseLogging: false,
				resolveLinkTos: true,
				maxSearchWindow: Consts.CatchUpDefaultReadBatchSize,
				subscriptionName: String.Empty
			);
	}
}
