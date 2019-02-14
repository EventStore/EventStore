using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages.Persisted.Commands {
	public struct PersistedProjectionVersion {
		public long Id;
		public long Epoch;
		public long Version;

		public static implicit operator ProjectionVersion(PersistedProjectionVersion source) {
			return new ProjectionVersion(source.Id, source.Epoch, source.Version);
		}

		public static implicit operator PersistedProjectionVersion(ProjectionVersion source) {
			return new PersistedProjectionVersion {
				Epoch = source.Epoch,
				Id = source.ProjectionId,
				Version = source.Version
			};
		}
	}
}
