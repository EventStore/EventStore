using System;

namespace EventStore.Client.PersistentSubscriptions {
	public sealed class PersistentSubscriptionSettings {
		/// <summary>
		/// Whether the <see cref="PersistentEventStoreSubscription"></see> should resolve linkTo events to their linked events.
		/// </summary>
		public readonly bool ResolveLinkTos;

		/// <summary>
		/// Which event position in the stream the subscription should start from.
		/// </summary>
		public readonly StreamRevision StartFrom;

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
		public readonly string NamedConsumerStrategy;

		public PersistentSubscriptionSettings(bool resolveLinkTos = false, StreamRevision? startFrom = null,
			bool extraStatistics = false, TimeSpan? messageTimeout = null, int maxRetryCount = 500,
			int liveBufferSize = 500, int readBatchSize = 10, int historyBufferSize = 20,
			TimeSpan? checkPointAfter = null, int minCheckPointCount = 10, int maxCheckPointCount = 1000,
			int maxSubscriberCount = 0, string namedConsumerStrategy = SystemConsumerStrategies.RoundRobin) {
			messageTimeout ??= TimeSpan.FromSeconds(30);
			checkPointAfter ??= TimeSpan.FromSeconds(2);
			startFrom ??= StreamRevision.End;

			if (messageTimeout.Value < TimeSpan.Zero || messageTimeout.Value.TotalMilliseconds > int.MaxValue) {
				throw new ArgumentOutOfRangeException(
					nameof(messageTimeout),
					$"{nameof(messageTimeout)} must be greater than {TimeSpan.Zero} and less than or equal to {TimeSpan.FromMilliseconds(int.MaxValue)}");
			}

			if (checkPointAfter.Value < TimeSpan.Zero || checkPointAfter.Value.TotalMilliseconds > int.MaxValue) {
				throw new ArgumentOutOfRangeException(
					nameof(checkPointAfter),
					$"{nameof(checkPointAfter)} must be greater than {TimeSpan.Zero} and less than or equal to {TimeSpan.FromMilliseconds(int.MaxValue)}");
			}

			ResolveLinkTos = resolveLinkTos;
			StartFrom = startFrom.Value;
			ExtraStatistics = extraStatistics;
			MessageTimeout = messageTimeout.Value;
			MaxRetryCount = maxRetryCount;
			LiveBufferSize = liveBufferSize;
			ReadBatchSize = readBatchSize;
			HistoryBufferSize = historyBufferSize;
			CheckPointAfter = checkPointAfter.Value;
			MinCheckPointCount = minCheckPointCount;
			MaxCheckPointCount = maxCheckPointCount;
			MaxSubscriberCount = maxSubscriberCount;
			NamedConsumerStrategy = namedConsumerStrategy;
		}
	}
}
