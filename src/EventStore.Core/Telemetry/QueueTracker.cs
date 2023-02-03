namespace EventStore.Core.Telemetry {
	// Composite tracker for tracking the various things that queues want to track.
	// i.e.
	//   - Duration items spent in the queue
	//   - Processing time of items at the end of the queue
	public class QueueTracker {
		private readonly string _name;
		private readonly IDurationMaxTracker _queueingDurationTracker;
		private readonly IQueueProcessingTracker _queueProcessingTracker;
		private readonly IClock _clock;

		public QueueTracker(
			string name,
			IDurationMaxTracker queueingDurationTracker,
			IQueueProcessingTracker processingDurationTracker,
			IClock clock = null) {

			_name = name;
			_queueingDurationTracker = queueingDurationTracker;
			_queueProcessingTracker = processingDurationTracker;
			_clock = clock ?? Clock.Instance;
		}

		public static QueueTracker NoOp { get; } = new(
			name: "NoOp",
			new DurationMaxTracker.NoOp(),
			new QueueProcessingTracker.NoOp());

		public string Name => _name;

		public Instant Now => _clock.Now;

		public Instant RecordMessageDequeued(Instant enqueuedAt) {
			return _queueingDurationTracker.RecordNow(enqueuedAt);
		}

		public Instant RecordMessageProcessed(Instant processingStartedAt, string messageType) {
			return _queueProcessingTracker.RecordNow(processingStartedAt, messageType);
		}
	}
}
