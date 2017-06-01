namespace EventStore.Projections.Core.Common
{
    public static class EventTypes
    {
        public const string ProjectionCreated = "$ProjectionCreated";
        public const string ProjectionDeleted = "$ProjectionDeleted";
        public const string ProjectionsInitialized = "$ProjectionsInitialized";
        public const string ProjectionUpdated = "$ProjectionUpdated";
    }
}
