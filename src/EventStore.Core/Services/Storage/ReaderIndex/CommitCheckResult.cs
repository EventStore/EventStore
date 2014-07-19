namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public struct CommitCheckResult
    {
        public readonly CommitDecision Decision;
        public readonly string EventStreamId;
        public readonly int CurrentVersion;
        public readonly int StartEventNumber;
        public readonly int EndEventNumber;
        public readonly bool IsSoftDeleted;

        public CommitCheckResult(CommitDecision decision, 
                                 string eventStreamId, 
                                 int currentVersion, 
                                 int startEventNumber, 
                                 int endEventNumber,
                                 bool isSoftDeleted)
        {
            Decision = decision;
            EventStreamId = eventStreamId;
            CurrentVersion = currentVersion;
            StartEventNumber = startEventNumber;
            EndEventNumber = endEventNumber;
            IsSoftDeleted = isSoftDeleted;
        }
    }
}