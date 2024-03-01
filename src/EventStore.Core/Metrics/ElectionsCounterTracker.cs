using EventStore.Core.Bus;
using EventStore.Core.Messages;


namespace EventStore.Core.Metrics;

public interface IElectionCounterTracker {
	void IncrementCounter();
}

public class ElectionsCounterTracker : IElectionCounterTracker,
	IHandle<ElectionMessage.ElectionsDone> {
	private readonly CounterSubMetric _electionsCounter;
	private readonly IElectionCounterTracker _tracker;

	public ElectionsCounterTracker(CounterSubMetric electionsCounter) {
		_electionsCounter = electionsCounter;
	}

	public ElectionsCounterTracker(IElectionCounterTracker tracker) {
		_tracker = tracker;
	}

	public void IncrementCounter() {
		_electionsCounter.Add(1);
	}

	public class NoOp : IElectionCounterTracker {
		public void IncrementCounter() {
		}
	}

	public void Handle(ElectionMessage.ElectionsDone message) {
		_tracker.IncrementCounter();
	}
}
