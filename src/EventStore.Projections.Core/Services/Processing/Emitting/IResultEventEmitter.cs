using EventStore.Projections.Core.Services.Processing.Checkpointing;
using EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents;

namespace EventStore.Projections.Core.Services.Processing.Emitting;

public interface IResultEventEmitter {
	EmittedEventEnvelope[] ResultUpdated(string partition, string result, CheckpointTag at);
}
