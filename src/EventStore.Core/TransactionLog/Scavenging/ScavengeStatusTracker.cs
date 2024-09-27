using System;
using EventStore.Core.Metrics;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IScavengeStatusTracker {
		IDisposable StartActivity(string name);
	}

	public class ScavengeStatusTracker : IScavengeStatusTracker {
		private static ActivityStatusSubMetric _subMetric;

		public ScavengeStatusTracker(StatusMetric metric) {
			_subMetric = new("Scavenge", metric);
		}

		public IDisposable StartActivity(string name) =>
			_subMetric?.StartActivity(name + " Phase");

		public class NoOp : IScavengeStatusTracker {
			public IDisposable StartActivity(string name) => null;
		}
	}
}
