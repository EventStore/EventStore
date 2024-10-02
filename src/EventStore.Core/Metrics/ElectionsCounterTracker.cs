// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;
using EventStore.Core.Messages;


namespace EventStore.Core.Metrics;

public interface IElectionCounterTracker : IHandle<ElectionMessage.ElectionsDone> {
}

public class ElectionsCounterTracker : IElectionCounterTracker {
	private readonly CounterSubMetric _electionsCounter;

	public ElectionsCounterTracker(CounterSubMetric electionsCounter) {
		_electionsCounter = electionsCounter;
	}

	public void Handle(ElectionMessage.ElectionsDone message) {
		_electionsCounter.Add(1);
	}

	public class NoOp : IElectionCounterTracker {
		public void Handle(ElectionMessage.ElectionsDone message) {
		}
	}
}
