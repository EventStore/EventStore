// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable
using System.Collections.Generic;
using EventStore.Core.Metrics;
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
