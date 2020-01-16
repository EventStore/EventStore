using System;

namespace EventStore.Client {
	public class StreamNotFoundException : Exception {
		/// <summary>
		/// The name of the deleted stream.
		/// </summary>
		public readonly string Stream;

		/// <summary>
		/// Constructs a new instance of <see cref="StreamNotFoundException"/>.
		/// </summary>
		/// <param name="stream">The name of the deleted stream.</param>
		public StreamNotFoundException(string stream, Exception exception = null)
			: base($"Event stream '{stream}' was not found.", exception) {
			if (stream == null) {
				throw new ArgumentNullException(nameof(stream));
			}

			Stream = stream;
		}
	}
}
