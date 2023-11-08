using System;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using Google.Protobuf;

// ReSharper disable once CheckNamespace
namespace EventStore.Client.Messages {
	partial class EventRecord {
		public EventRecord(string eventStreamId, long eventNumber, byte[] eventId, string eventType, int dataContentType, int metadataContentType, byte[] data, byte[] metadata, long created, long createdEpoch) {
			AssignValues(eventStreamId, eventNumber, eventId, eventType, dataContentType, metadataContentType, data, metadata, created, createdEpoch);
		}

		private void AssignValues(string eventStreamId, long eventNumber, byte[] eventId, string eventType, int dataContentType,
			int metadataContentType, byte[] data, byte[] metadata, long created, long createdEpoch)
		{
			EventStreamId = eventStreamId;
			EventNumber = eventNumber;
			EventId = ByteString.CopyFrom(eventId);
			EventType = eventType;
			DataContentType = dataContentType;
			MetadataContentType = metadataContentType;
			Data = ByteString.CopyFrom(data);
			Metadata = ByteString.CopyFrom(metadata);
			Created = created;
			CreatedEpoch = createdEpoch;
		}

		public EventRecord(Core.Data.EventRecord eventRecord) {
			if (eventRecord == null) return;
			AssignValues(
				eventRecord.EventStreamId,
				eventRecord.EventNumber,
				eventRecord.EventId.ToByteArray(),
				eventRecord.EventType,
				eventRecord.IsJson ? 1 : 0,
				eventRecord.IsJson ? 1 : 0,
				eventRecord.Data.ToArray(),
				eventRecord.Metadata.ToArray(),
				eventRecord.TimeStamp.ToBinary(),
				(long)(eventRecord.TimeStamp - new DateTime(1970, 1, 1)).TotalMilliseconds
				);
		}

		public EventRecord(Core.Data.EventRecord eventRecord, long eventNumber) {
			if (eventRecord == null) return;
			AssignValues(
				eventRecord.EventStreamId,
				eventNumber,
				eventRecord.EventId.ToByteArray(),
				eventRecord.EventType,
				eventRecord.IsJson ? 1 : 0,
				eventRecord.IsJson ? 1 : 0,
				eventRecord.Data.ToArray(),
				eventRecord.Metadata.ToArray(),
				eventRecord.TimeStamp.ToBinary(),
				(long)(eventRecord.TimeStamp - new DateTime(1970, 1, 1)).TotalMilliseconds
			);
		}
	}

	partial class ResolvedEvent {
		public ResolvedEvent(EventRecord @event, EventRecord link, long commitPosition, long preparePosition)
		{
			if (@event != null) Event = @event;
			if (link != null) Link = link;
			CommitPosition = commitPosition;
			PreparePosition = preparePosition;
		}

		public ResolvedEvent(Core.Data.ResolvedEvent pair)
			: this(pair.Event != null ? new EventRecord(pair.Event) : null,
				pair.Link != null ? new EventRecord(pair.Link) : null,
				pair.OriginalPosition?.CommitPosition ?? -1,
				pair.OriginalPosition?.PreparePosition ?? -1) {
		}
	}

	partial class SubscribeToStream {
		public SubscribeToStream(string eventStreamId, bool resolveLinkTos)
		{
			EventStreamId = eventStreamId;
			ResolveLinkTos = resolveLinkTos;
		}
	}

	partial class CheckpointReached {
		public CheckpointReached(long commitPosition, long preparePosition)
		{
			CommitPosition = commitPosition;
			PreparePosition = preparePosition;
		}
	}

	partial class SubscriptionConfirmation {
		public SubscriptionConfirmation(long lastCommitPosition, long lastEventNumber)
		{
			LastCommitPosition = lastCommitPosition;
			LastEventNumber = lastEventNumber;
		}
	}

	partial class StreamEventAppeared {
		public StreamEventAppeared(ResolvedEvent @event) {
			Event = @event;
		}
	}

	partial class SubscriptionDropped {
		public SubscriptionDropped(SubscriptionDropped.Types.SubscriptionDropReason reason)
		{
			Reason = reason;
		}
	}

	partial class NotHandled {
		public NotHandled(ClientMessage.NotHandled source) {
			Reason = source.Reason switch {
				ClientMessage.NotHandled.Types.NotHandledReason.NotReady => Types.NotHandledReason.NotReady,
				ClientMessage.NotHandled.Types.NotHandledReason.TooBusy => Types.NotHandledReason.TooBusy,
				ClientMessage.NotHandled.Types.NotHandledReason.NotLeader => Types.NotHandledReason.NotLeader,
				ClientMessage.NotHandled.Types.NotHandledReason.IsReadOnly => Types.NotHandledReason.IsReadOnly,
				_ => throw new ArgumentOutOfRangeException()
			};
			//this is horrible and only for transport compatibility purposes
			if(source.LeaderInfo != null) AdditionalInfo = new Types.LeaderInfo(source.LeaderInfo.Http).ToByteString();
		}
		partial class Types {
			partial class LeaderInfo {
				public LeaderInfo(string externalTcpAddress, int externalTcpPort, string httpAddress, int httpPort, string externalSecureTcpAddress, int externalSecureTcpPort)
				{
					ExternalTcpAddress = externalTcpAddress;
					ExternalTcpPort = externalTcpPort;
					HttpAddress = httpAddress;
					HttpPort = httpPort;
					ExternalSecureTcpAddress = externalSecureTcpAddress;
					ExternalSecureTcpPort = externalSecureTcpPort;
				}

				public LeaderInfo(EndPoint httpEndPoint) {
					HttpAddress = httpEndPoint.GetHost();
					HttpPort = httpEndPoint.GetPort();
				}
			}
		}
	}

	partial class ScavengeDatabaseResponse {
		public ScavengeDatabaseResponse(ScavengeDatabaseResponse.Types.ScavengeResult result, string scavengeId)
		{
			Result = result;
			ScavengeId = scavengeId;
		}
	}

	partial class IdentifyClient {
		public IdentifyClient(int version, string connectionName) {
			Version = version;
			if (connectionName != null) ConnectionName = connectionName;
		}
	}
}
