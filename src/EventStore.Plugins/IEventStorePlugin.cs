namespace EventStore.Plugins
{
    public interface IEventStorePlugin
    {
        string Name { get; }
        string Version { get; }
        IEventStoreServiceFactory GetStrategyFactory();
    }
}
