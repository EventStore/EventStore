using System;
using EventStore.Core.Telemetry;

namespace EventStore.Core.Index {
	public interface IIndexStatusTracker {
		IDisposable StartMerging();
		IDisposable StartScavenging();
	}

	public class IndexStatusTracker : IIndexStatusTracker {
		private readonly ActivityStatusSubMetric _metric;

		public IndexStatusTracker(StatusMetric metric) {
			_metric = new("Index", metric);
		}

		public IDisposable StartMerging() => _metric.StartActivity("Merging");
		public IDisposable StartScavenging() => _metric.StartActivity("Scavenging");

		public class NoOp : IIndexStatusTracker {
			public IDisposable StartMerging() => null;

			public IDisposable StartScavenging() => null;
		}
	}

}
