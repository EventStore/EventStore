using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents stream metadata as a series of properties for system
	/// data and a <see cref="StreamMetadata"/> object for user metadata.
	/// </summary>
	public struct StreamMetadataResult {
		/// <summary>
		/// The name of the stream.
		/// </summary>
		public readonly string Stream;

		/// <summary>
		/// True if the stream is soft-deleted.
		/// </summary>
		public readonly bool IsStreamDeleted;

		/// <summary>
		/// The version of the metadata format.
		/// </summary>
		public readonly long MetastreamVersion;

		/// <summary>
		/// A <see cref="StreamMetadata"/> containing user-specified metadata.
		/// </summary>
		public readonly StreamMetadata StreamMetadata;

		/// <summary>
		/// Constructs a new instance of <see cref="StreamMetadataResult"/>.
		/// </summary>
		/// <param name="stream">The name of the stream.</param>
		/// <param name="isStreamDeleted">True if the stream is soft-deleted.</param>
		/// <param name="metastreamVersion">The version of the metadata format.</param>
		/// <param name="streamMetadata">A <see cref="StreamMetadataResult"/> containing user-specified metadata.</param>
		public StreamMetadataResult(string stream, bool isStreamDeleted, long metastreamVersion,
			StreamMetadata streamMetadata) {
			Ensure.NotNullOrEmpty(stream, "stream");

			Stream = stream;
			IsStreamDeleted = isStreamDeleted;
			MetastreamVersion = metastreamVersion;
			StreamMetadata = streamMetadata;
		}
	}
}
