using System;
using System.Collections.Generic;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.LogAbstraction {
	public interface INameIndexConfirmer<TValue> : IDisposable {
		void InitializeWithConfirmed(INameLookup<TValue> source);

		/// Entries are confirmed once they are replicated.
		/// Once confirmed, the entry can be persisted.
		// trying quite hard not to use the word 'commit' since it has other uses.
		bool Confirm(IList<IPrepareLogRecord<TValue>> replicatedPrepares);
	}
}
