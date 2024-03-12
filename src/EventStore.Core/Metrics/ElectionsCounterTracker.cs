using EventStore.Core.Bus;
using EventStore.Core.Messages;
using Serilog;


namespace EventStore.Core.Metrics;

public interface IElectionCounterTracker : IHandle<ElectionMessage.ElectionsDone> {
}

public class ElectionsCounterTracker : IElectionCounterTracker{
	private readonly CounterSubMetric _electionsCounter;
	private readonly IElectionCounterTracker _electionsCounterTracker;
	public ElectionsCounterTracker(CounterSubMetric electionsCounter) {
		_electionsCounter = electionsCounter;
	}

	// public ElectionsCounterTracker() {
	// }

	public ElectionsCounterTracker(IElectionCounterTracker tracker) {
		_electionsCounterTracker = tracker;
	}

	public class NoOp : IElectionCounterTracker {
		public void Handle(ElectionMessage.ElectionsDone message) {

		}
	}

	public void Handle(ElectionMessage.ElectionsDone message) {
		// Increment the counter
		//_electionsCounter.Add(1);
	}
}
