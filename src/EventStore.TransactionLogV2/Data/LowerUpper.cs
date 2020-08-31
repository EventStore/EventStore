namespace EventStore.Core.TransactionLogV2.Data {
	public struct Range {
		public readonly long Lower;
		public readonly long Upper;

		public Range(long lower, long upper) {
			Lower = lower;
			Upper = upper;
		}
	}
}
