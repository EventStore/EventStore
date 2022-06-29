using System;

namespace EventStore.Core.TransactionLog.Scavenging {
	// store a range per chunk so that the calculator can definitely get a timestamp range for each event
	// that is guaranteed to contain the real timestamp of that event.
	public struct ChunkTimeStampRange : IEquatable<ChunkTimeStampRange> {
		public ChunkTimeStampRange(DateTime min, DateTime max) {
			Min = min;
			Max = max;
		}

		public DateTime Min { get; }

		public DateTime Max { get; }

		public bool Equals(ChunkTimeStampRange other) =>
			Min == other.Min &&
			Max == other.Max;

		// avoid the default, reflection based, implementations if we ever need to call these
		public override int GetHashCode() => throw new NotImplementedException();
		public override bool Equals(object other) => throw new NotImplementedException();
	}
}
