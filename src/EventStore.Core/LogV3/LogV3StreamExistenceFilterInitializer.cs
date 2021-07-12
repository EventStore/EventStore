using System;
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

		public void Initialize(INameExistenceFilter filter) {
			if (!_streamNames.TryGetLastValue(out var sourceLastStreamId))
				return;

			var startStreamId = Math.Max(LogV3SystemStreams.FirstRealStream, (uint)filter.CurrentCheckpoint);
			for (var streamId = startStreamId; streamId <= sourceLastStreamId; streamId += LogV3SystemStreams.StreamInterval) {
				if (!_streamNames.TryGetName(streamId, out var name))
					throw new Exception($"NameExistenceFilter: this should never happen. could not find {streamId} in source");
				filter.Add(name, streamId);
			}
		}
	}
}
