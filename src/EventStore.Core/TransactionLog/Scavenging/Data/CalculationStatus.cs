namespace EventStore.Core.TransactionLog.Scavenging {
	public enum CalculationStatus {
		// Invalid
		None = 0,

		// Needs to be processed by calculator next time
		Active = 1,

		// Can be skipped over by calculator next time
		Archived = 2,

		// Can be deleted by cleaner when there are no chunks pending execution
		Spent = 3,
	}
}
