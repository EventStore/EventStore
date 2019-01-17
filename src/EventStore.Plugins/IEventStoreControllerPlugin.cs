namespace EventStore.Plugins
{
    public interface IEventStoreControllerPlugin
    {
        string Name { get; }
        string Version { get; }
        IEventStoreControllerFactory GetStrategyFactory();
    }
}
