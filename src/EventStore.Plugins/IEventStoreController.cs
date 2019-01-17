namespace EventStore.Plugins
{
    public interface IEventStoreController
    {
        string StartUriTemplate { get; }
        string StopUriTemplate { get; }
        string StatsUriTemplate { get; }
    }
}
