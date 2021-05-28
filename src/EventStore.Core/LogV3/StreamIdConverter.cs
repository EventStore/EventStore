using System;

namespace EventStore.Core.LogV3 {
	/// Converts between StreamIds and their event number in the streams stream.
	public static class StreamIdConverter {
		static long _offset = LogV3SystemStreams.FirstRealStream / LogV3SystemStreams.StreamInterval;

		public static long ToStreamId(long index) {
			return (index + _offset) * LogV3SystemStreams.StreamInterval;
		}

		public static long ToEventNumber(long streamId) {
			if (streamId % LogV3SystemStreams.StreamInterval != 0)
				throw new ArgumentOutOfRangeException(nameof(streamId), "streamId must be even");

			return streamId / LogV3SystemStreams.StreamInterval - _offset;
		}
	}
}
