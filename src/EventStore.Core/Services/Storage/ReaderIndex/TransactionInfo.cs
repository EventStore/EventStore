namespace EventStore.Core.Services.Storage.ReaderIndex {
	public struct TransactionInfo<TStreamId> {
		public readonly int TransactionOffset;
		public readonly TStreamId EventStreamId;

		public TransactionInfo(int transactionOffset, TStreamId eventStreamId) {
			TransactionOffset = transactionOffset;
			EventStreamId = eventStreamId;
		}
	}
}
