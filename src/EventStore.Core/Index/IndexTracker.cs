#nullable enable
using System.Collections.Generic;
using EventStore.Core.Telemetry;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Index {
	public interface IIndexTracker {
		void OnIndexed<TStreamId>(List<IPrepareLogRecord<TStreamId>> prepares);
	}

	public class IndexTracker : IIndexTracker {
		private readonly CounterSubMetric _indexedEvents;

		public IndexTracker(CounterSubMetric indexedEvents) {
			_indexedEvents = indexedEvents;
		}

		public void OnIndexed<TStreamId>(List<IPrepareLogRecord<TStreamId>> prepares) {
			_indexedEvents.Add(prepares.Count);
		}

		public class NoOp : IIndexTracker {
			public void OnIndexed<TStreamId>(List<IPrepareLogRecord<TStreamId>> record) {
			}
		}
	}
}
