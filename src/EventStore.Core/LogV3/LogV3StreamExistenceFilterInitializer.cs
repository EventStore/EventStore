using System;
using System.Collections.Generic;
using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	/// Populates a stream existence filter by iterating through the names
	/// In V3 the the bloom filter checkpoint is the last processed stream number.
	public class LogV3StreamExistenceFilterInitializer : INameExistenceFilterInitializer {
		private readonly INameLookup<StreamId> _streamNames;

		public LogV3StreamExistenceFilterInitializer(INameLookup<StreamId> streamNames) {
			_streamNames = streamNames;
		}

		private IEnumerable<(string name, long checkpoint)> EnumerateNames(long lastCheckpoint) {
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
