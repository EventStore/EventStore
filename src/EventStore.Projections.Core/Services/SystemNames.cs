namespace EventStore.Projections.Core.Services {
	public static class ProjectionEventTypes {
		public const string ProjectionCheckpoint = "$ProjectionCheckpoint";
		public const string PartitionCheckpoint = "$Checkpoint";
		public const string StreamTracked = "$StreamTracked";

		public const string ProjectionCreated = "$ProjectionCreated";
		public const string ProjectionDeleted = "$ProjectionDeleted";
		public const string ProjectionsInitialized = "$ProjectionsInitialized";
		public const string ProjectionUpdated = "$ProjectionUpdated";
	}
}
