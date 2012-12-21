namespace EventStore.Projections.Core.Services.Processing
{
    public interface IEventWriter
    {
        void EmitEvents(EmittedEvent[] events);
    }
}