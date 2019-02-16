using System;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Settings for <see cref="EventStoreCatchUpSubscription"/>.
	/// </summary>
	public class CatchUpSubscriptionSettings {
		/// <summary>
		/// The maximum amount of events to cache when processing from a live subscription. Going above this value will drop the subscription.
		/// </summary>
		public readonly int MaxLiveQueueSize;

		/// <summary>
		/// The number of events to read per batch when reading the history.
		/// </summary>
		public readonly int ReadBatchSize;

		/// <summary>
		/// Enables verbose logging on the subscription.
		/// </summary>
		public readonly bool VerboseLogging;

		/// <summary>
		/// Whether to resolve link events.
		/// </summary>
		public readonly bool ResolveLinkTos;

		/// <summary>
		/// The name of the subscription.
		/// </summary>
		public readonly string SubscriptionName;

		///<summary>
		/// Returns default settings.
		///</summary>
		public static readonly CatchUpSubscriptionSettings Default = new CatchUpSubscriptionSettings(
			Consts.CatchUpDefaultMaxPushQueueSize,
			Consts.CatchUpDefaultReadBatchSize,
			verboseLogging: false,
			resolveLinkTos: true,
			subscriptionName: String.Empty
		);

		/// <summary>
		/// Constructs a <see cref="CatchUpSubscriptionSettings"/> object.
		/// </summary>
		/// <param name="maxLiveQueueSize">The maximum amount of events to buffer when processing from a live subscription. Going above this amount will drop the subscription.</param>
		/// <param name="readBatchSize">The number of events to read per batch when reading through history.</param>
		/// <param name="verboseLogging">Enables verbose logging on the subscription.</param>
		/// <param name="resolveLinkTos">Whether to resolve link events.</param>
		/// <param name="subscriptionName">The name of the subscription.</param>
		public CatchUpSubscriptionSettings(int maxLiveQueueSize, int readBatchSize, bool verboseLogging,
			bool resolveLinkTos, string subscriptionName = "") {
			Ensure.Positive(readBatchSize, "readBatchSize");
			Ensure.Positive(maxLiveQueueSize, "maxLiveQueueSize");
			if (readBatchSize > ClientApiConstants.MaxReadSize)
				throw new ArgumentException(string.Format(
					"Read batch size should be less than {0}. For larger reads you should page.",
					ClientApiConstants.MaxReadSize));
			MaxLiveQueueSize = maxLiveQueueSize;
			ReadBatchSize = readBatchSize;
			VerboseLogging = verboseLogging;
			ResolveLinkTos = resolveLinkTos;
			SubscriptionName = subscriptionName;
		}
	}
}
