using System;

namespace EventStore.Grpc {
	public struct StreamMetadataResult : IEquatable<StreamMetadataResult> {
		public readonly string StreamName;
		public readonly bool StreamDeleted;
		public readonly StreamMetadata Metadata;
		public readonly StreamRevision? MetastreamRevision;

		public override int GetHashCode() =>
			HashCode.Hash.Combine(StreamName).Combine(Metadata).Combine(MetastreamRevision);

		public bool Equals(StreamMetadataResult other) =>
			StreamName == other.StreamName && StreamDeleted == other.StreamDeleted &&
			Equals(Metadata, other.Metadata) && Nullable.Equals(MetastreamRevision, other.MetastreamRevision);

		public override bool Equals(object obj) => obj is StreamMetadataResult other && Equals(other);
		public static bool operator ==(StreamMetadataResult left, StreamMetadataResult right) => left.Equals(right);
		public static bool operator !=(StreamMetadataResult left, StreamMetadataResult right) => !left.Equals(right);

		public static StreamMetadataResult None(string streamName) => new StreamMetadataResult(streamName);
		public static StreamMetadataResult Create(string streamName, StreamRevision revision,
			StreamMetadata metadata) {
			if (metadata == null) throw new ArgumentNullException(nameof(metadata));

			return new StreamMetadataResult(streamName, revision, metadata);
		}

		private StreamMetadataResult(string streamName, StreamRevision? metastreamRevision = default,
			StreamMetadata metadata = default, bool streamDeleted = false) {
			if (streamName == null) {
				throw new ArgumentNullException(nameof(streamName));
			}

			StreamName = streamName;
			StreamDeleted = streamDeleted;
			Metadata = metadata;
			MetastreamRevision = metastreamRevision;
		}
	}
}
