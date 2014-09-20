using System;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Builds a <see cref="PersistentSubscriptionSettings"/> object.
    /// </summary>
    public class PersistentSubscriptionSettingsBuilder
    {
        private bool _resolveLinkTos;
        private int  _startFrom;
        private bool _latencyStatistics;
        private TimeSpan _timeout;
        private int _readBatchSize;
        private int _maxRetryCount;
        private int _liveBufferSize;
        private bool _preferRoundRobin;
        private int _historyBufferSize;

        /// <summary>
        /// Creates a new <see cref="PersistentSubscriptionSettingsBuilder"></see> object
        /// </summary>
        /// <returns>a new <see cref="PersistentSubscriptionSettingsBuilder"></see> object</returns>
        public static PersistentSubscriptionSettingsBuilder Create()
        {
            return new PersistentSubscriptionSettingsBuilder(false, 
                                                             -1, 
                                                             false, 
                                                             TimeSpan.FromSeconds(30),
                                                             500,
                                                             500,
                                                             10,
                                                             20,
                                                             true);
        }


        private PersistentSubscriptionSettingsBuilder(bool resolveLinkTos, int startFrom, bool latencyStatistics, TimeSpan timeout,
                                                      int historyBufferSize, int liveBufferSize, int maxRetryCount, int readBatchSize, bool preferRoundRobin)
        {
            _resolveLinkTos = resolveLinkTos;
            _startFrom = startFrom;
            _latencyStatistics = latencyStatistics;
            _timeout = timeout;
            _historyBufferSize = historyBufferSize;
            _liveBufferSize = liveBufferSize;
            _maxRetryCount = maxRetryCount;
            _readBatchSize = readBatchSize;
            _preferRoundRobin = preferRoundRobin;
        }


        /// <summary>
        /// Sets the option to include further latency statistics. These statistics have a cost and should not be used
        /// in high performance situations.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder WithExtraLatencyStatistics()
        {
            _latencyStatistics = true;
            return this;
        }

        /// <summary>
        /// Sets the option to resolve linktos on events that are found for this subscription.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder ResolveLinkTos()
        {
            _resolveLinkTos = true;
            return this;
        }

        /// <summary>
        /// Sets the option to not resolve linktos on events that are found for this subscription.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder DoNotResolveLinkTos()
        {
            _resolveLinkTos = false;
            return this;
        }

        /// <summary>
        /// If set the subscription will prefer if possible to round robin between the clients that
        /// are connected.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder PreferRoundRobin()
        {
            _preferRoundRobin = true;
            return this;
        }

        /// <summary>
        /// If set the subscription will prefer if possible to dispatch only to a single of the connected
        /// clients. If however the buffer limits are reached on that client it will begin sending to other 
        /// clients.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder PreferDispatchToSingle()
        {
            _preferRoundRobin = false;
            return this;
        }

        /// <summary>
        /// Sets that the subscription should start from the beginning of the stream.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder StartFromBeginning()
        {
            _startFrom = 0;
            return this;
        }

        /// <summary>
        /// Sets that the subscription should start from a specified location of the stream.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder StartFrom(int position)
        {
            _startFrom = position;
            return this;
        }

        /// <summary>
        /// Sets the timeout for a message (will be retried if an ack is not received within this timespan)
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder WithMessageTimeoutOf(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        /// <summary>
        /// Sets the number of times a message should be retried before being considered a bad message
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder WithMaxRetriesOf(int count)
        {
            Ensure.Nonnegative(count, "count");
            _maxRetryCount = count;
            return this;
        }

        /// <summary>
        /// Sets the size of the live buffer for the subscription. This is the buffer used 
        /// to cache messages while sending messages as they happen. The count is
        /// in terms of the number of messages to cache.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder WithLiveBufferSizeOf(int count)
        {
            Ensure.Nonnegative(count, "count");
            _liveBufferSize = count;
            return this;
        }


        /// <summary>
        /// Sets the size of the read batch used when paging in history for the subscription
        /// sizes should not be too big ...
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder WithReadBatchOf(int count)
        {
            Ensure.Nonnegative(count, "count");
            _readBatchSize = count;
            return this;
        }


        /// <summary>
        /// Sets the size of the read batch used when paging in history for the subscription
        /// sizes should not be too big ...
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder WithHistoryBufferSizeOf(int count)
        {
            Ensure.Nonnegative(count, "count");
            _historyBufferSize = count;
            return this;
        }

        /// <summary>
        /// Sets that the subscription should start from where the stream is when the subscription is first connected.
        /// </summary>
        /// <returns>A new <see cref="PersistentSubscriptionSettingsBuilder"></see></returns>
        public PersistentSubscriptionSettingsBuilder StartFromCurrent()
        {
            _startFrom = -1;
            return this;
        }

        /// <summary>
        /// Builds a <see cref="PersistentSubscriptionSettings"/> object from a <see cref="PersistentSubscriptionSettingsBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="PersistentSubscriptionSettingsBuilder"/> from which to build a <see cref="PersistentSubscriptionSettingsBuilder"/></param>
        /// <returns></returns>
        public static implicit operator PersistentSubscriptionSettings(PersistentSubscriptionSettingsBuilder builder)
        {
            return new PersistentSubscriptionSettings(builder._resolveLinkTos,
                builder._startFrom,
                builder._latencyStatistics,
                builder._timeout,
                builder._maxRetryCount,
                builder._liveBufferSize,
                builder._readBatchSize,
                builder._historyBufferSize,
                builder._preferRoundRobin);
        }
    }
}