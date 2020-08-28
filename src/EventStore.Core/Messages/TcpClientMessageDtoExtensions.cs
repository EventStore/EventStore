using System;
using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Core.Messages {
	public partial class TcpClientMessageDto {
		public partial class ResolvedIndexedEvent {
			public ResolvedIndexedEvent(TransactionLog.Data.EventRecord eventRecord, TransactionLog.Data.EventRecord linkRecord)
				: this(eventRecord != null ? new EventRecord(eventRecord) : null,
					linkRecord != null ? new EventRecord(linkRecord) : null) {
			}
		}

		public partial class ResolvedEvent {
			public ResolvedEvent(Data.ResolvedEvent pair)
				: this(pair.Event != null ? new EventRecord(pair.Event) : null,
					pair.Link != null ? new EventRecord(pair.Link) : null,
					pair.OriginalPosition.Value.CommitPosition,
					pair.OriginalPosition.Value.PreparePosition) {
			}
		}

		public partial class EventRecord {
			public EventRecord(TransactionLog.Data.EventRecord eventRecord) {
				EventStreamId = eventRecord.EventStreamId;
				EventNumber = eventRecord.EventNumber;
				EventId = eventRecord.EventId.ToByteArray();
				EventType = eventRecord.EventType;
				Data = eventRecord.Data.ToArray();
				Created = eventRecord.TimeStamp.ToBinary();
				Metadata = eventRecord.Metadata.ToArray();
				var isJson = eventRecord.IsJson;
				DataContentType = isJson ? 1 : 0;
				MetadataContentType = isJson ? 1 : 0;
				CreatedEpoch = (long)(eventRecord.TimeStamp - new DateTime(1970, 1, 1)).TotalMilliseconds;
			}

			public EventRecord(TransactionLog.Data.EventRecord eventRecord, long eventNumber) {
				EventStreamId = eventRecord.EventStreamId;
				EventNumber = eventNumber;
				EventId = eventRecord.EventId.ToByteArray();
				EventType = eventRecord.EventType;
				Data = eventRecord.Data.ToArray();
				Created = eventRecord.TimeStamp.ToBinary();
				Metadata = eventRecord.Metadata.ToArray();
				var isJson = eventRecord.IsJson;
				DataContentType = isJson ? 1 : 0;
				MetadataContentType = isJson ? 1 : 0;
				CreatedEpoch = (long)(eventRecord.TimeStamp - new DateTime(1970, 1, 1)).TotalMilliseconds;
			}
		}

		public partial class NotHandled {
			public partial class LeaderInfo {
				public LeaderInfo(EndPoint externalTcpEndPoint, EndPoint externalSecureTcpEndPoint,
					EndPoint httpEndPoint) {
					ExternalTcpAddress = externalTcpEndPoint == null ? null : externalTcpEndPoint.GetHost();
					ExternalTcpPort = externalTcpEndPoint == null ? (int?) null : externalTcpEndPoint.GetPort();
					ExternalSecureTcpAddress = externalSecureTcpEndPoint == null
						? null
						: externalSecureTcpEndPoint.GetHost();
					ExternalSecureTcpPort =
						externalSecureTcpEndPoint == null ? (int?)null : externalSecureTcpEndPoint.GetPort();
					HttpAddress = httpEndPoint.GetHost();
					HttpPort = httpEndPoint.GetPort();
				}
			}
		}
	}
}
