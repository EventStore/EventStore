namespace EventStore.Projections.Core.Services.Processing
{
    public interface IEventWriter
    {
        void ValidateOrderAndEmitEvents(EmittedEvent[] events);
    }
}