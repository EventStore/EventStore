using System;
using EventStore.ClientAPI.Common.Utils;
namespace EventStore.ClientAPI
{
    /// <summary>
    /// Settings for <see cref="EventStoreCatchUpSubscription"/>
    /// </summary>
    public class CatchUpSubscriptionSettings
    {
        /// <summary>
        /// The maximum amount to cache when processing from live subscription. Going above will drop subscription.
        /// </summary>
        public readonly int MaxLiveQueueSize;

        /// <summary>
        /// The number of events to read per batch when reading history
        /// </summary>
        public readonly int ReadBatchSize;

        /// <summary>
        /// Enables verbose logging on the subscription
        /// </summary>
        public readonly bool VerboseLogging;

        /// <summary>
        /// Whether or not to resolve link events
        /// </summary>
        public readonly bool ResolveLinkTos;

        ///<summary>
        /// Returns default settings
        ///</summary>
        public static readonly CatchUpSubscriptionSettings Default = new CatchUpSubscriptionSettings(Consts.CatchUpDefaultMaxPushQueueSize, Consts.CatchUpDefaultReadBatchSize,  false, true);

        /// <summary>
        /// Constructs a <see cref="CatchUpSubscriptionSettings"/> object
        /// </summary>
        /// <param name="maxLiveQueueSize">The maximum amount to buffer when processing from live subscription. Going above will drop subscription.</param>
        /// <param name="readBatchSize">The number of events to read per batch when reading history</param>
        /// <param name="verboseLogging">Enables verbose logging on the subscription</param>
        /// <param name="resolveLinkTos">Whether or not to resolve link events</param>
        public CatchUpSubscriptionSettings(int maxLiveQueueSize, int readBatchSize, bool verboseLogging, bool resolveLinkTos) {
            Ensure.Positive(readBatchSize, "readBatchSize");
            Ensure.Positive(maxLiveQueueSize, "maxLiveQueueSize");
            if (readBatchSize > Consts.MaxReadSize) throw new ArgumentException(string.Format("Read batch size should be less than {0}. For larger reads you should page.", Consts.MaxReadSize));
            MaxLiveQueueSize = maxLiveQueueSize;
            ReadBatchSize = readBatchSize;
            VerboseLogging = verboseLogging;
            ResolveLinkTos = resolveLinkTos;
        }
    }
}