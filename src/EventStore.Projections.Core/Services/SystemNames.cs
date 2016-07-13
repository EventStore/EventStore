namespace EventStore.Projections.Core.Services
{
    public static class ProjectionEventTypes
    {
        public const string ProjectionCheckpoint = "$ProjectionCheckpoint";
        public const string PartitionCheckpoint = "$Checkpoint";
        public const string StreamTracked = "$StreamTracked";
    }
}
