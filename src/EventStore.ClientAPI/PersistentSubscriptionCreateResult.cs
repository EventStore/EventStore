using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// A \Persistent Subscription Create Result is the result of a single operation creating a
    /// persistent subscription in the event store
    /// </summary>
    public class PersistentSubscriptionCreateResult
    {
        /// <summary>
        /// The <see cref="PersistentSubscriptionCreateStatus"/> representing the status of this create attempt
        /// </summary>
        public readonly PersistentSubscriptionCreateStatus Status;

        internal PersistentSubscriptionCreateResult(PersistentSubscriptionCreateStatus Status)
        {
        }
    }
}