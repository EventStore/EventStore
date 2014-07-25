using EventStore.ClientAPI.ClientOperations;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents a persistent subscription to some particular stream.
    /// </summary>
    public class PersistentSubscription
    {
        internal PersistentSubscription(PersistentSubscriptionOperation persistentSubscriptionOperation, string streamId, long lastCommitPosition, int? lastEventNumber)
        {
            throw new System.NotImplementedException();
        }
    }
}