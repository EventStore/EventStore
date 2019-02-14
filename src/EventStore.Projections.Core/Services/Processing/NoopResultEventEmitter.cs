using System;

namespace EventStore.Projections.Core.Services.Processing {
	public class NoopResultEventEmitter : IResultEventEmitter {
		public EmittedEventEnvelope[] ResultUpdated(string partition, string result, CheckpointTag at) {
			throw new NotSupportedException("No results are expected from the projection");
		}
	}
}
