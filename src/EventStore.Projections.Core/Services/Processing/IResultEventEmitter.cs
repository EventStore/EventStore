namespace EventStore.Projections.Core.Services.Processing {
	public interface IResultEventEmitter {
		EmittedEventEnvelope[] ResultUpdated(string partition, string result, CheckpointTag at);
	}
}
