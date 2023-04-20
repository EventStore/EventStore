using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace EventStore.Core.Telemetry {
	public class CounterSubMetric<T> where T : struct {
		private readonly Counter<T> _metric;
		private readonly KeyValuePair<string, object> _tag;

		public CounterSubMetric(Counter<T> metric, KeyValuePair<string, object> tag) {
			_metric = metric;
			_tag = tag;
		}

		public void Add(T delta) {
			_metric.Add(delta, _tag);
		}
	}
}
