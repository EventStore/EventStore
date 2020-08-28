namespace EventStore.Core.TransactionLog.Data {
	public struct Range {
		public readonly long Lower;
		public readonly long Upper;

		public Range(long lower, long upper) {
			Lower = lower;
			Upper = upper;
		}
	}
}
