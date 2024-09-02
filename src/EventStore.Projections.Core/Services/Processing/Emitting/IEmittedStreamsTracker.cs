using EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents;

namespace EventStore.Projections.Core.Services.Processing.Emitting;

public interface IEmittedStreamsTracker {
	void TrackEmittedStream(EmittedEvent[] emittedEvents);
	void Initialize();
}
