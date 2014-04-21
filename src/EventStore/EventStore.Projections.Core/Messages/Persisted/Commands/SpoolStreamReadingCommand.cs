namespace EventStore.Projections.Core.Messages.Persisted.Commands
{
    public class SpoolStreamReadingCommand
    {
        public string SubscriptionId { get; set; }
        public string StreamId { get; set; }
        public int CatalogSequenceNumber { get; set; }
        public long LimitingCommitPosition { get; set; }
    }
}