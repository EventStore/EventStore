using EventStore.Projections.Core.Services.Processing.Emitting;

namespace EventStore.Projections.Core.Services.Processing.Checkpointing {
	public interface IEventWriter {
		void ValidateOrderAndEmitEvents(EmittedEventEnvelope[] events);
	}
}
