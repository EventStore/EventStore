#nullable enable

using EventStore.Core.Metrics;
using EventStore.Core.TransactionLog.Chunks;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EventStore.Core.TransactionLog;

public interface ITransactionFileTrackerFactory {
	ITransactionFileTracker GetOrAdd(string name);
	static readonly ITransactionFileTrackerFactory NoOp = new NoOp();
}

file class NoOp : ITransactionFileTrackerFactory {
	public ITransactionFileTracker GetOrAdd(string name) => ITransactionFileTracker.NoOp;
}
