using System;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Messages {
	public partial class TcpClientMessageDto {
		public partial class ResolvedIndexedEvent {
			public ResolvedIndexedEvent(TransactionLogV2.Data.EventRecord eventRecord, TransactionLogV2.Data.EventRecord linkRecord)
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
			public EventRecord(TransactionLogV2.Data.EventRecord eventRecord) {
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

			public EventRecord(TransactionLogV2.Data.EventRecord eventRecord, long eventNumber) {
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
				public LeaderInfo(EndPoint tcpEndPoint, bool isTcpEndPointSecure, EndPoint httpEndPoint) {
					if (isTcpEndPointSecure) {
						ExternalSecureTcpAddress = tcpEndPoint?.GetHost();
						ExternalSecureTcpPort = tcpEndPoint?.GetPort();
					} else {
						ExternalTcpAddress = tcpEndPoint?.GetHost();
						ExternalTcpPort = tcpEndPoint?.GetPort();
					}

					HttpAddress = httpEndPoint.GetHost();
					HttpPort = httpEndPoint.GetPort();
				}
			}
		}
	}

	public static class FilterExtensions {
		public static IEventFilter ToEventFilter(this TcpClientMessageDto.Filter filter) {
			if (filter == null || filter.Data.Length == 0) {
				return new EventFilter.AlwaysAllowStrategy();
			}

			return filter.Context switch {
				TcpClientMessageDto.Filter.FilterContext.EventType when filter.Type ==
				                                                        TcpClientMessageDto.Filter.FilterType.Prefix =>
				EventFilter.EventType.Prefixes(filter.Data),
				TcpClientMessageDto.Filter.FilterContext.EventType when filter.Type ==
				                                                        TcpClientMessageDto.Filter.FilterType.Regex =>
				EventFilter.EventType.Regex(filter.Data[0]),
				TcpClientMessageDto.Filter.FilterContext.StreamId when filter.Type ==
				                                                       TcpClientMessageDto.Filter.FilterType.Prefix =>
				EventFilter.StreamName.Prefixes(filter.Data),
				TcpClientMessageDto.Filter.FilterContext.StreamId when filter.Type ==
				                                                       TcpClientMessageDto.Filter.FilterType.Regex =>
				EventFilter.StreamName.Regex(filter.Data[0]),
				_ => throw new Exception() // Invalid filter
			};
		}
	}
}
