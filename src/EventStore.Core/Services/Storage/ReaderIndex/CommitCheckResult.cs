namespace EventStore.Core.Services.Storage.ReaderIndex {
	public struct CommitCheckResult {
		public readonly CommitDecision Decision;
		public readonly string EventStreamId;
		public readonly long CurrentVersion;
		public readonly long StartEventNumber;
		public readonly long EndEventNumber;
		public readonly bool IsSoftDeleted;

		public CommitCheckResult(CommitDecision decision,
			string eventStreamId,
			long currentVersion,
			long startEventNumber,
			long endEventNumber,
			bool isSoftDeleted) {
			Decision = decision;
			EventStreamId = eventStreamId;
			CurrentVersion = currentVersion;
			StartEventNumber = startEventNumber;
			EndEventNumber = endEventNumber;
			IsSoftDeleted = isSoftDeleted;
		}
	}
}
