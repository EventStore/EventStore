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
		public readonly long CommitPosition;
		public readonly long PreparePosition;
		public readonly bool IsJson;

		public EventRecord(
			string eventStreamId,
			Guid eventId,
			StreamRevision eventNumber,
			IDictionary<string, string> metadata,
			byte[] data,
			byte[] customMetadata) {
			EventStreamId = eventStreamId;
			EventId = eventId;
			EventNumber = eventNumber;
			Data = data;
			Metadata = customMetadata;
			EventType = metadata[Constants.Metadata.Type];
			Created = DateTime.FromBinary(Convert.ToInt64(metadata[Constants.Metadata.Created]));
			IsJson = bool.Parse(metadata[Constants.Metadata.IsJson]);
		}
	}
}
