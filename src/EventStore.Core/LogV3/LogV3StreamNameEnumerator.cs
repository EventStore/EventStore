using System;
using System.Collections.Generic;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class LogV3StreamNameEnumerator : INameEnumerator {
		private readonly INameLookup<StreamId> _streamNames;

		public LogV3StreamNameEnumerator(INameLookup<StreamId> streamNames) {
			_streamNames = streamNames;
		}

		public IEnumerable<(string name, long checkpoint)> EnumerateNames(long lastCheckpoint) {
			if (lastCheckpoint < 0L) {
				foreach (var name in LogV3SystemStreams.EnumerateVirtualStreamNames()) {
					yield return (name, -1L);
					yield return (SystemStreams.MetastreamOf(name), -1L);
				}
			}

			if (!_streamNames.TryGetLastValue(out var lastValue)) {
				yield break;
			}

			var startEventNumber = Math.Max(0L, lastCheckpoint);
			var lastEventNumber = StreamIdConverter.ToEventNumber(lastValue);

			for (var eventNumber = startEventNumber; eventNumber <= lastEventNumber; eventNumber++) {
				var name = _streamNames.LookupName(StreamIdConverter.ToStreamId(eventNumber));
				yield return (name, eventNumber);
				//qq i think we wont need to emit the metastreams in the end
				// instead they are implicitly in the filter (and the places that have
				// access are wrapped in metahandlers)
				yield return (SystemStreams.MetastreamOf(name), eventNumber);
			}
		}
	}
}
