namespace EventStore.Core.TransactionLogV2.Data {
	public static class EventNumber {
		public const long DeletedStream = long.MaxValue;
		public const long Invalid = int.MinValue;
	}
}
