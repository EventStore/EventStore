namespace EventStore.Core.Services.PersistentSubscription
{
    public enum NakAction
    {
        Unknown = 0,
        Poison = 1,
        Retry = 2,
        Skip = 3,
        Stop = 4
    }
}