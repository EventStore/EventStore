using System;
using EventStore.ClientAPI.Common;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents the settings for a <see cref="PersistentEventStoreSubscription"></see>. You should not use
	/// this directly, but instead created via a <see cref="PersistentSubscriptionSettingsBuilder"></see>.
	/// </summary>
	public class PersistentSubscriptionSettings {
		/// <summary>
		/// Creates a new <see cref="PersistentSubscriptionSettingsBuilder"></see> object
		/// </summary>
		/// <returns>a new <see cref="PersistentSubscriptionSettingsBuilder"></see> object</returns>
		public static PersistentSubscriptionSettingsBuilder Create() {
			return new PersistentSubscriptionSettingsBuilder(false,
				-1,
				false,
				TimeSpan.FromSeconds(30),
				500,
				500,
				10,
				20,
				TimeSpan.FromSeconds(2),
				10,
				1000,
				0,
				SystemConsumerStrategies.RoundRobin);
		}


		/// <summary>
		/// Whether the <see cref="PersistentEventStoreSubscription"></see> should resolve linkTo events to their linked events.
		/// </summary>
		public readonly bool ResolveLinkTos;

		/// <summary>
		/// Which event position in the stream the subscription should start from.
		/// </summary>
		public readonly long StartFrom;

		/// <summary>
		/// Whether to track latency statistics on this subscription.
		/// </summary>
		public readonly bool ExtraStatistics;

		/// <summary>
		/// The amount of time after which to consider a message as timedout and retried.
		/// </summary>
		public readonly TimeSpan MessageTimeout;

		/// <summary>
		/// The maximum number of retries (due to timeout) before a message is considered to be parked.
		/// </summary>
		public int MaxRetryCount;

		/// <summary>
		/// The size of the buffer (in-memory) listening to live messages as they happen before paging occurs.
		/// </summary>
		public int LiveBufferSize;

		/// <summary>
		/// The number of events read at a time when paging through history.
		/// </summary>
		public int ReadBatchSize;

		/// <summary>
		/// The number of events to cache when paging through history.
		/// </summary>
		public int HistoryBufferSize;

		/// <summary>
		/// The amount of time to try to checkpoint after.
		/// </summary>
		public readonly TimeSpan CheckPointAfter;

		/// <summary>
		/// The minimum number of messages to write to a checkpoint.
		/// </summary>
		public readonly int MinCheckPointCount;

		/// <summary>
		/// The maximum number of messages not checkpointed before forcing a checkpoint.
		/// </summary>
		public readonly int MaxCheckPointCount;

		/// <summary>
		/// The maximum number of subscribers allowed.
		/// </summary>
		public readonly int MaxSubscriberCount;

		/// <summary>
		/// The strategy to use for distributing events to client consumers. See <see cref="SystemConsumerStrategies"/> for system supported strategies.
		/// </summary>
		public string NamedConsumerStrategy;

		/// <summary>
		/// Constructs a new <see cref="PersistentSubscriptionSettings"></see>.
		/// </summary>
		internal PersistentSubscriptionSettings(bool resolveLinkTos, long startFrom, bool extraStatistics,
			TimeSpan messageTimeout,
			int maxRetryCount, int liveBufferSize, int readBatchSize, int historyBufferSize,
			TimeSpan checkPointAfter, int minCheckPointCount, int maxCheckPointCount,
			int maxSubscriberCount, string namedConsumerStrategy) {
			if (messageTimeout.TotalMilliseconds > Int32.MaxValue)
				throw new ArgumentException("milliseconds must be less or equal to than int32.MaxValue",
					nameof(messageTimeout));
			if (checkPointAfter.TotalMilliseconds > Int32.MaxValue)
				throw new ArgumentException("milliseconds must be less or equal to than int32.MaxValue",
					nameof(checkPointAfter));
			MessageTimeout = messageTimeout;
			ResolveLinkTos = resolveLinkTos;
			StartFrom = startFrom;
			ExtraStatistics = extraStatistics;
			MaxRetryCount = maxRetryCount;
			LiveBufferSize = liveBufferSize;
			ReadBatchSize = readBatchSize;
			HistoryBufferSize = historyBufferSize;
			CheckPointAfter = checkPointAfter;
			MinCheckPointCount = minCheckPointCount;
			MaxCheckPointCount = maxCheckPointCount;
			MaxSubscriberCount = maxSubscriberCount;
			NamedConsumerStrategy = namedConsumerStrategy;
		}
	}
}
