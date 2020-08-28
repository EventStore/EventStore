namespace EventStore.Core.TransactionLog.Data {
	public static class EventNumber {
		public const long DeletedStream = long.MaxValue;
		public const long Invalid = int.MinValue;
	}
}
