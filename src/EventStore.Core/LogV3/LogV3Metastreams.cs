using System;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV3 {
	public class LogV3Metastreams : IMetastreamLookup<long> {
		public bool IsMetaStream(long streamId) =>
			streamId % 2 == 1;

		// in v2 this prepends "$$"
		public long MetaStreamOf(long streamId) {
			if (IsMetaStream(streamId))
				throw new ArgumentException($"{streamId} is already a metastream", nameof(streamId));
			return streamId + 1;
		}

		// in v2 this drops the first two characters, 
		public long OriginalStreamOf(long streamId) {
			if (!IsMetaStream(streamId))
				throw new ArgumentException($"{streamId} is not a metastream", nameof(streamId));
			return streamId - 1;
		}
	}
}
