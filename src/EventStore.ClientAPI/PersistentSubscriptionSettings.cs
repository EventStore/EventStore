namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents the settings for a <see cref="PersistentSubscription"></see> you should
    /// normally not use this class directly but instead use a <see cref="PersistentSubscriptionSettingsBuilder"></see>
    /// </summary>
    public class PersistentSubscriptionSettings
    {
        /// <summary>
        /// Whether or not the <see cref="PersistentSubscription"></see> should resolve linkTo events to their linked events.
        /// </summary>
        public readonly bool ResolveLinkTos;

        /// <summary>
        /// Whether the subscription should start from the beginning of the stream, if false it will start where the stream is 
        /// currently located.
        /// </summary>
        public readonly bool StartFromBeginning;

        /// <summary>
        /// Whether or not in depth latency statistics should be tracked on this subscription.
        /// </summary>
        public readonly bool LatencyStatistics;

        /// <summary>
        /// Constructs a new <see cref="PersistentSubscriptionSettings"></see>
        /// </summary>
        internal PersistentSubscriptionSettings(bool resolveLinkTos, bool startFromBeginning, bool latencyStatistics)
        {
            ResolveLinkTos = resolveLinkTos;
            StartFromBeginning = startFromBeginning;
            LatencyStatistics = latencyStatistics;
        }
    }
}