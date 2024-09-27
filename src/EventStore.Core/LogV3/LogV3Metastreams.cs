using System;
using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class LogV3Metastreams : IMetastreamLookup<StreamId> {
		public bool IsMetaStream(StreamId streamId) =>
			streamId % 2 == 1;

		// in v2 this prepends "$$"
		public StreamId MetaStreamOf(StreamId streamId) {
			if (IsMetaStream(streamId))
				throw new ArgumentException($"{streamId} is already a metastream", nameof(streamId));
			return streamId + 1;
		}

		// in v2 this drops the first two characters, 
		public StreamId OriginalStreamOf(StreamId streamId) {
			if (!IsMetaStream(streamId))
				throw new ArgumentException($"{streamId} is not a metastream", nameof(streamId));
			return streamId - 1;
		}
	}
}
