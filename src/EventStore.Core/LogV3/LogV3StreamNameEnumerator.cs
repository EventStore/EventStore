using System;
using System.Collections.Generic;
using EventStore.Core.Index;
using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class LogV3StreamNameEnumerator : INameEnumerator {
		private readonly INameLookup<StreamId> _streamNames;

		//qq possibly this doesn't have to be a member, in which case we can
		// constrct this earlier in the abstractor
		public LogV3StreamNameEnumerator(INameLookup<StreamId> streamNames) {
			_streamNames = streamNames;
		}

		public void SetTableIndex(ITableIndex tableIndex) { }

		private IEnumerable<(string name, long checkpoint)> EnumerateNames(long lastCheckpoint) {
			//qq we dont need to fill virtual streams into the filter here,
			// but we do need to make sure that they are either implicitly always in the filter
			// or do get added to the filter when we write to them (almost certainly we will go with the former)
			var source = _streamNames;

			if (!source.TryGetLastValue(out var sourceLastStreamId)) {
				yield break;
			}

			var startStreamId = Math.Max(LogV3SystemStreams.FirstRealStream, (uint)lastCheckpoint);

			for (var streamId = startStreamId; streamId <= sourceLastStreamId; streamId += LogV3SystemStreams.StreamInterval) {
				if (!source.TryGetName(streamId, out var name))
					throw new Exception($"NameExistenceFilter: this should never happen. could not find {streamId} in source");
				yield return (name, streamId);
			}
		}

		public void Initialize(INameExistenceFilter filter) {
			var lastCheckpoint = filter.CurrentCheckpoint;
			foreach (var (name, checkpoint) in EnumerateNames(lastCheckpoint)) {
				filter.Add(name, checkpoint);
			}
		}
	}
}
