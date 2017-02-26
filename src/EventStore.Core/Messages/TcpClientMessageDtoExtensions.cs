using System;
using System.Net;
using EventStore.Core.Helpers;

namespace EventStore.Core.Messages
{
    public partial class TcpClientMessageDto
    {
        public partial class ResolvedIndexedEvent
        {
            public ResolvedIndexedEvent(Data.EventRecord eventRecord, Data.EventRecord linkRecord)
                : this(eventRecord != null ? new EventRecord(eventRecord) : null,
                       linkRecord != null ? new EventRecord(linkRecord) : null)
            {
            }
        }

        public partial class ResolvedEvent
        {
            public ResolvedEvent(Data.ResolvedEvent pair)
                : this(pair.Event != null ? new EventRecord(pair.Event) : null,
                       pair.Link != null ? new EventRecord(pair.Link) : null,
                       pair.OriginalPosition.Value.CommitPosition,
                       pair.OriginalPosition.Value.PreparePosition)
            {
            }
        }

        public partial class EventRecord
        {
            public EventRecord(Data.EventRecord eventRecord)
            {
                EventStreamId = eventRecord.EventStreamId;
                EventNumber = eventRecord.EventNumber;
                EventId = eventRecord.EventId.ToByteArray();
                EventType = eventRecord.EventType;
                Data = eventRecord.Data;
                Created = eventRecord.TimeStamp.ToBinary();
                Metadata = eventRecord.Metadata;
                var isJson = eventRecord.IsJson;
                DataContentType = isJson ? 1 : 0;
                MetadataContentType = isJson ? 1 : 0;
                CreatedEpoch = (long) (eventRecord.TimeStamp - new DateTime (1970, 1, 1)).TotalMilliseconds;
            }
        }

        public partial class NotHandled
        {
            public partial class MasterInfo
            {
                public MasterInfo(IPEndPoint externalTcpEndPoint, IPEndPoint externalSecureTcpEndPoint, IPEndPoint externalHttpEndPoint)
                {
                    ExternalTcpAddress = externalTcpEndPoint.Address.ToString();
                    ExternalTcpPort = externalTcpEndPoint.Port;
                    ExternalSecureTcpAddress = externalSecureTcpEndPoint == null ? null : externalSecureTcpEndPoint.Address.ToString();
                    ExternalSecureTcpPort = externalSecureTcpEndPoint == null ? (int?)null : externalSecureTcpEndPoint.Port;
                    ExternalHttpAddress = externalHttpEndPoint.Address.ToString();
                    ExternalHttpPort = externalHttpEndPoint.Port;
                }
            }
        }

        public partial class ResolvedIndexedEventV1
        {
            public ResolvedIndexedEventV1(Data.EventRecord eventRecord, Data.EventRecord linkRecord)
                : this(eventRecord != null ? new EventRecordV1(eventRecord) : null,
                       linkRecord != null ? new EventRecordV1(linkRecord) : null)
            {
            }
        }

        public partial class ResolvedEventV1
        {
            public ResolvedEventV1(Data.ResolvedEvent pair)
                : this(pair.Event != null ? new EventRecordV1(pair.Event) : null,
                       pair.Link != null ? new EventRecordV1(pair.Link) : null,
                       pair.OriginalPosition.Value.CommitPosition,
                       pair.OriginalPosition.Value.PreparePosition)
            {
            }
        }

        public partial class EventRecordV1
        {
            public EventRecordV1(Data.EventRecord eventRecord)
            {
                EventStreamId = eventRecord.EventStreamId;
                EventNumber = ExpectedVersionConverter.ConvertTo32Bit(eventRecord.EventNumber);
                EventId = eventRecord.EventId.ToByteArray();
                EventType = eventRecord.EventType;
                Data = eventRecord.Data;
                Created = eventRecord.TimeStamp.ToBinary();
                Metadata = eventRecord.Metadata;
                var isJson = eventRecord.IsJson;
                DataContentType = isJson ? 1 : 0;
                MetadataContentType = isJson ? 1 : 0;
                CreatedEpoch = (long) (eventRecord.TimeStamp - new DateTime (1970, 1, 1)).TotalMilliseconds;
            }
        }
    }
}