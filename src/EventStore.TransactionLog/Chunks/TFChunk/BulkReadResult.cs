namespace EventStore.Core.TransactionLog.Chunks.TFChunk {
	public struct BulkReadResult {
		public readonly int OldPosition;
		public readonly int BytesRead;
		public readonly bool IsEOF;

		public BulkReadResult(int oldPosition, int bytesRead, bool isEof) {
			OldPosition = oldPosition;
			BytesRead = bytesRead;
			IsEOF = isEof;
		}
	}
}
