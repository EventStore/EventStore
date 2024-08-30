using EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents;

namespace EventStore.Projections.Core.Services.Processing.Checkpointing {
	public interface IEventWriter {
		void ValidateOrderAndEmitEvents(EmittedEventEnvelope[] events);
	}
}
