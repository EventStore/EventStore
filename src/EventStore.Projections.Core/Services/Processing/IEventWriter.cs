namespace EventStore.Projections.Core.Services.Processing {
	public interface IEventWriter {
		void ValidateOrderAndEmitEvents(EmittedEventEnvelope[] events);
	}
}
