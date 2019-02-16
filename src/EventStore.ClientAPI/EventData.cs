using EventStore.ClientAPI.Internal;
using System;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents an event to be written.
	/// </summary>
	public sealed class EventData {
		/// <summary>
		/// The ID of the event, used as part of the idempotent write check.
		/// </summary>
		public readonly Guid EventId;

		/// <summary>
		/// The name of the event type. It is strongly recommended that these
		/// use lowerCamelCase if projections are to be used.
		/// </summary>
		public readonly string Type;

		/// <summary>
		/// Flag indicating whether the data and metadata are JSON.
		/// </summary>
		public readonly bool IsJson;

		/// <summary>
		/// The raw bytes of the event data.
		/// </summary>
		public readonly byte[] Data;

		/// <summary>
		/// The raw bytes of the event metadata.
		/// </summary>
		public readonly byte[] Metadata;

		/// <summary>
		/// Constructs a new <see cref="EventData" />.
		/// </summary>
		/// <param name="eventId">The ID of the event, used as part of the idempotent write check.</param>
		/// <param name="type">The name of the event type. It is strongly recommended that these
		/// use lowerCamelCase if projections are to be used.</param>
		/// <param name="isJson">Flag indicating whether the data and metadata are JSON.</param>
		/// <param name="data">The raw bytes of the event data.</param>
		/// <param name="metadata">The raw bytes of the event metadata.</param>
		public EventData(Guid eventId, string type, bool isJson, byte[] data, byte[] metadata) {
			EventId = eventId;
			Type = type;
			IsJson = isJson;
			Data = data ?? Empty.ByteArray;
			Metadata = metadata ?? Empty.ByteArray;
		}
	}
}
