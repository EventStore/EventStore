using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Internal;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents stream metadata as a series of properties for system
	/// data and a byte array for user metadata.
	/// </summary>
	public struct RawStreamMetadataResult {
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
		/// A byte array containing user-specified metadata.
		/// </summary>
		public readonly byte[] StreamMetadata;

		/// <summary>
		/// Constructs a new instance of <see cref="RawStreamMetadataResult"/>.
		/// </summary>
		/// <param name="stream">The name of the stream.</param>
		/// <param name="isStreamDeleted">True if the stream is soft-deleted.</param>
		/// <param name="metastreamVersion">The version of the metadata format.</param>
		/// <param name="streamMetadata">A byte array containing user-specified metadata.</param>
		public RawStreamMetadataResult(string stream, bool isStreamDeleted, long metastreamVersion,
			byte[] streamMetadata) {
			Ensure.NotNullOrEmpty(stream, "stream");

			Stream = stream;
			IsStreamDeleted = isStreamDeleted;
			MetastreamVersion = metastreamVersion;
			StreamMetadata = streamMetadata ?? Empty.ByteArray;
		}
	}
}
