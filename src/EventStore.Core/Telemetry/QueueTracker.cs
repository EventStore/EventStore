namespace EventStore.Core.Telemetry {
	// Composite tracker for tracking the various things that queues want to track.
	public class QueueTracker {
		private readonly string _name;
		private readonly IDurationMaxTracker _queueingDurationTracker;
		private readonly IClock _clock;

		public QueueTracker(
			string name,
			IDurationMaxTracker queueingDurationTracker,
			IClock clock = null) {

			_name = name;
			_queueingDurationTracker = queueingDurationTracker;
			_clock = clock ?? Clock.Instance;
		}

		public static QueueTracker NoOp { get; } = new(
			name: "NoOp",
			new DurationMaxTracker.NoOp());

		public string Name => _name;

		public Instant Now => _clock.Now;

		public void RecordMessageDequeued(Instant enqueuedAt) {
			_queueingDurationTracker.RecordNow(enqueuedAt);
		}
	}
}
