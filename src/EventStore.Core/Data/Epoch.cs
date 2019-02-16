using System;

namespace EventStore.Core.Data {
	public class Epoch {
		public readonly long EpochPosition;
		public readonly int EpochNumber;
		public readonly Guid EpochId;

		public Epoch(long epochPosition, int epochNumber, Guid epochId) {
			EpochPosition = epochPosition;
			EpochNumber = epochNumber;
			EpochId = epochId;
		}
	}
}
