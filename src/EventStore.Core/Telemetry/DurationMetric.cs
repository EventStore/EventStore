using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Telemetry {
	public class DurationMetric {
		private readonly Histogram<double> _histogram;
		private readonly IClock _clock;

		public DurationMetric(Meter meter, string name, IClock clock = null) {
			_clock = clock ?? Clock.Instance;
			_histogram = meter.CreateHistogram<double>(name, "seconds");
		}

		public Duration Start(string durationName) =>
			new(this, durationName, _clock.UtcNow);

		public void Record(
			DateTime start,
			KeyValuePair<string, object> tag1,
			KeyValuePair<string, object> tag2) {

			var elapsed = _clock.UtcNow - start;
			_histogram.Record(elapsed.TotalSeconds, tag1, tag2);
		}
	}
}
