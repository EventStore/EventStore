using System;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents the settings for a <see cref="PersistentEventStoreSubscription"></see> you should
    /// normally not use this class directly but instead use a <see cref="PersistentSubscriptionSettingsBuilder"></see>
    /// </summary>
    public class PersistentSubscriptionSettings
    {
        /// <summary>
        /// Whether or not the <see cref="PersistentEventStoreSubscription"></see> should resolve linkTo events to their linked events.
        /// </summary>
        public readonly bool ResolveLinkTos;

        /// <summary>
        /// Where the subscription should start from (position)
        /// </summary>
        public readonly int StartFrom;

        /// <summary>
        /// Whether or not in depth latency statistics should be tracked on this subscription.
        /// </summary>
        public readonly bool LatencyStatistics;

        /// <summary>
        /// The amount of time after which a message should be considered to be timedout and retried.
        /// </summary>
        public readonly TimeSpan MessageTimeout;

        /// <summary>
        /// The maximum number of retries (due to timeout) before a message get considered to be parked
        /// </summary>
        public int MaxRetryCount;

        /// <summary>
        /// The size of the buffer listening to live messages as they happen
        /// </summary>
        public int LiveBufferSize;

        /// <summary>
        /// The number of events read at a time when paging in history
        /// </summary>
        public int ReadBatchSize;

        /// <summary>
        /// The number of events to cache when paging through history
        /// </summary>
        public int HistoryBufferSize;

        /// <summary>
        /// Whether the subscription should prefer round robin between clients of
        /// sending to a single client if possible.
        /// </summary>
        public bool PreferRoundRobin;

        /// <summary>
        /// The amount of time to try to checkpoint after 
        /// </summary>
        public readonly TimeSpan CheckPointAfter;

        /// <summary>
        /// The minimum number of messages to checkpoint
        /// </summary>
        public readonly int MinCheckPointCount;

        /// <summary>
        /// The maximum number of messages to checkpoint if this number is a reached a checkpoint will be forced.
        /// </summary>
        public readonly int MaxCheckPointCount;

        /// <summary>
        /// Constructs a new <see cref="PersistentSubscriptionSettings"></see>
        /// </summary>
        internal PersistentSubscriptionSettings(bool resolveLinkTos, int startFrom, bool latencyStatistics, TimeSpan messageTimeout,
                                                int maxRetryCount, int liveBufferSize, int readBatchSize, int historyBufferSize,
                                                bool preferRoundRobin, TimeSpan checkPointAfter, int minCheckPointCount, int maxCheckPointCount)
        {
            MessageTimeout = messageTimeout;
            ResolveLinkTos = resolveLinkTos;
            StartFrom = startFrom;
            LatencyStatistics = latencyStatistics;
            MaxRetryCount = maxRetryCount;
            LiveBufferSize = liveBufferSize;
            ReadBatchSize = readBatchSize;
            HistoryBufferSize = historyBufferSize;
            PreferRoundRobin = preferRoundRobin;
            CheckPointAfter = checkPointAfter;
            MinCheckPointCount = minCheckPointCount;
            MaxCheckPointCount = maxCheckPointCount;
        }

    }
}