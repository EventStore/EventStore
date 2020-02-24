using System;
using System.Net.Http.Headers;

namespace EventStore.Client {
	public sealed class EventData {
		public readonly ReadOnlyMemory<byte> Data;
		public readonly Uuid EventId;
		public readonly ReadOnlyMemory<byte> Metadata;
		public readonly string Type;
		public readonly string ContentType;

		private EventData(Uuid eventId, string type, string contentType) {
			if (eventId == Uuid.Empty) {
				throw new ArgumentOutOfRangeException(nameof(eventId));
			}

			if (type == null) {
				throw new ArgumentNullException(nameof(type));
			}

			if (contentType == null) {
				throw new ArgumentNullException(nameof(contentType));
			}

			MediaTypeHeaderValue.Parse(contentType);

			if (contentType != Constants.Metadata.ContentTypes.ApplicationJson &&
				contentType != Constants.Metadata.ContentTypes.ApplicationOctetStream) {
				throw new ArgumentOutOfRangeException(nameof(contentType), contentType,
					$"Only {Constants.Metadata.ContentTypes.ApplicationJson} or {Constants.Metadata.ContentTypes.ApplicationOctetStream} are acceptable values.");
			}

			EventId = eventId;
			Type = type;
			ContentType = contentType;
		}

		public EventData(Uuid eventId, string type, byte[] data, byte[] metadata = default,
			string contentType = Constants.Metadata.ContentTypes.ApplicationJson) : this(eventId, type, contentType) {

			Data = data ?? Array.Empty<byte>();
			Metadata = metadata ?? Array.Empty<byte>();
		}

		public EventData(Uuid eventId, string type, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte>? metadata = default,
			string contentType = Constants.Metadata.ContentTypes.ApplicationJson) : this(eventId, type, contentType) {

			Data = data;
			Metadata = metadata ?? Array.Empty<byte>();
		}
	}
}
