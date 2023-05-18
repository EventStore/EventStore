using System.Diagnostics;

namespace EventStore.Core.Telemetry;

public interface IQueueBusyTracker {
	void EnterBusy();
	void EnterIdle();
}

public class QueueBusyTracker : IQueueBusyTracker {
	private readonly Stopwatch _stopwatch = new();

	public QueueBusyTracker(AverageMetric metric, string label) {
		metric.Register(label, () => _stopwatch.Elapsed.TotalSeconds);
	}

	public void EnterBusy() => _stopwatch.Start();

	public void EnterIdle() => _stopwatch.Stop();

	public class NoOp : IQueueBusyTracker {
		public void EnterBusy() {
		}

		public void EnterIdle() {
		}
	}
}
