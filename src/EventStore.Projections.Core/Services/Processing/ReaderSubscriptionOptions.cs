namespace EventStore.Projections.Core.Services.Processing {
	public class ReaderSubscriptionOptions {
		private readonly long _checkpointUnhandledBytesThreshold;
		private readonly int? _checkpointProcessedEventsThreshold;
		private readonly int _checkpointAfterMs;
		private readonly bool _stopOnEof;
		private readonly int? _stopAfterNEvents;

		public ReaderSubscriptionOptions(
			long checkpointUnhandledBytesThreshold, int? checkpointProcessedEventsThreshold, int checkpointAfterMs,
			bool stopOnEof, int? stopAfterNEvents) {
			_checkpointUnhandledBytesThreshold = checkpointUnhandledBytesThreshold;
			_checkpointProcessedEventsThreshold = checkpointProcessedEventsThreshold;
			_checkpointAfterMs = checkpointAfterMs;
			_stopOnEof = stopOnEof;
			_stopAfterNEvents = stopAfterNEvents;
		}

		public long CheckpointUnhandledBytesThreshold {
			get { return _checkpointUnhandledBytesThreshold; }
		}

		public int? CheckpointProcessedEventsThreshold {
			get { return _checkpointProcessedEventsThreshold; }
		}

		public int CheckpointAfterMs {
			get { return _checkpointAfterMs; }
		}

		public bool StopOnEof {
			get { return _stopOnEof; }
		}

		public int? StopAfterNEvents {
			get { return _stopAfterNEvents; }
		}
	}
}
