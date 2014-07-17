namespace EventStore.Core.Services.PersistentSubscription
{
    public enum PersistentSubscriptionState
    {
        Idle,
        Push,
        TransitioningFromPullToPush,
        Pull,
        ShuttingDown
    }
}