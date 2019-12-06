using System;
using System.Collections.Generic;

namespace EventStore.Grpc {
	public class EventRecord {
		public readonly string EventStreamId;
		public readonly Guid EventId;
		public readonly StreamRevision EventNumber;
		public readonly string EventType;
		public readonly byte[] Data;
		public readonly byte[] Metadata;
		public readonly DateTime Created;
		public readonly Position Position;
		public readonly bool IsJson;

		public EventRecord(
			string eventStreamId,
			Guid eventId,
			StreamRevision eventNumber,
			Position position,
			IDictionary<string, string> metadata,
			byte[] data,
			byte[] customMetadata) {
			EventStreamId = eventStreamId;
			EventId = eventId;
			EventNumber = eventNumber;
			Position = position;
			Data = data;
			Metadata = customMetadata;
			EventType = metadata[Constants.Metadata.Type];
			Created = new DateTime(
				DateTime.UnixEpoch.Ticks + Convert.ToInt64(metadata[Constants.Metadata.Created]),
				DateTimeKind.Utc);
			IsJson = bool.Parse(metadata[Constants.Metadata.IsJson]);
		}
	}
}
