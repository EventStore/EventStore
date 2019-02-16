namespace EventStore.Core.Services.Storage.ReaderIndex {
	public enum CommitDecision {
		Ok,
		WrongExpectedVersion,
		Deleted,
		Idempotent,
		CorruptedIdempotency,
		InvalidTransaction
	}
}
