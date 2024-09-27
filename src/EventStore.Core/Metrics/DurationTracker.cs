namespace EventStore.Core.Metrics {

	public interface IDurationTracker {
		Duration Start();
	}

	public class DurationTracker : IDurationTracker {
		private readonly DurationMetric _metric;
		private readonly string _durationName;

		public DurationTracker(DurationMetric metric, string durationName) {
			_metric = metric;
			_durationName = durationName;
		}

		public Duration Start() => _metric.Start(_durationName);

		public class NoOp : IDurationTracker {
			public Duration Start() => Duration.Nil;
		}
	}
}
