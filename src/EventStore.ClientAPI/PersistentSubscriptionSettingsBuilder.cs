namespace EventStore.ClientAPI
{
    /// <summary>
    /// Builds a <see cref="PersistentSubscriptionSettings"/> object.
    /// </summary>
    public class PersistentSubscriptionSettingsBuilder
    {
        private readonly bool _resolveLinkTos;
        private readonly bool _startFromBeginning;
        private readonly bool _latencyStatistics;

        /// <summary>
        /// Creates a new <see cref="PersistentSubscriptionSettingsBuilder"></see> object
        /// </summary>
        /// <returns>a new <see cref="PersistentSubscriptionSettingsBuilder"></see> object</returns>
        public static PersistentSubscriptionSettingsBuilder Create()
        {
            return new PersistentSubscriptionSettingsBuilder(false, false, false);
        }

        /// <summary>
        /// Sets the option to include further latency statistics. These statistics have a cost and should not be used
        /// in high performance situations.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder WithExtraLatencyStatistics()
        {
            return new PersistentSubscriptionSettingsBuilder(_resolveLinkTos, _startFromBeginning, true);
        }

        /// <summary>
        /// Sets the option to resolve linktos on events that are found for this subscription.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder ResolveLinkTos()
        {
            return new PersistentSubscriptionSettingsBuilder(true, _startFromBeginning, _latencyStatistics);
        }

        /// <summary>
        /// Sets the option to not resolve linktos on events that are found for this subscription.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder DoNotResolveLinkTos()
        {
            return new PersistentSubscriptionSettingsBuilder(false, _startFromBeginning, _latencyStatistics);
        }

        /// <summary>
        /// Sets that the subscription should start from the beginning of the stream.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder StartFromBeginning()
        {
            return new PersistentSubscriptionSettingsBuilder(_resolveLinkTos, true, _latencyStatistics);
        }

        /// <summary>
        /// Sets that the subscription should start from where the stream is when the subscription is first connected.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder StartFromCurrent()
        {
            return new PersistentSubscriptionSettingsBuilder(_resolveLinkTos, false, _latencyStatistics);
        }

        private PersistentSubscriptionSettingsBuilder(bool resolveLinkTos, bool startFromBeginning, bool latencyStatistics)
        {
            _resolveLinkTos = resolveLinkTos;
            _startFromBeginning = startFromBeginning;
            _latencyStatistics = latencyStatistics;
        }

        /// <summary>
        /// Builds a <see cref="PersistentSubscriptionSettings"/> object from a <see cref="PersistentSubscriptionSettingsBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="PersistentSubscriptionSettingsBuilder"/> from which to build a <see cref="PersistentSubscriptionSettingsBuilder"/></param>
        /// <returns></returns>
        public static implicit operator PersistentSubscriptionSettings(PersistentSubscriptionSettingsBuilder builder)
        {
            return new PersistentSubscriptionSettings(builder._resolveLinkTos,
                builder._startFromBeginning,
                builder._latencyStatistics);
        }
    }
}