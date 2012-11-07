using System.Net;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages
{
    public partial class TcpClientMessageDto
    {
        public partial class StreamEventAppeared
        {
            public StreamEventAppeared(int eventNumber, PrepareLogRecord prepareLogRecord, long commitPosition)
            {
                EventStreamId = prepareLogRecord.EventStreamId;
                EventNumber = eventNumber;
                EventId = prepareLogRecord.EventId.ToByteArray();
                EventType = prepareLogRecord.EventType;
                Data = prepareLogRecord.Data;
                Metadata = prepareLogRecord.Metadata;
                PreparePosition = prepareLogRecord.LogPosition;
                CommitPosition = commitPosition;
            }
        }

        public partial class DeniedToRoute
        {
            public DeniedToRoute(IPEndPoint externalTcpEndPoint, IPEndPoint externalHttpEndPoint)
            {
                ExternalTcpAddress = externalTcpEndPoint.Address.ToString();
                ExternalTcpPort = externalTcpEndPoint.Port;
                ExternalHttpAddress = externalHttpEndPoint.Address.ToString();
                ExternalHttpPort = externalHttpEndPoint.Port;
            }
        }

        public partial class EventLinkPair
        {
            public EventLinkPair(Data.EventRecord eventRecord, Data.EventRecord linkRecord)
            {
                Event = new EventRecord(eventRecord);

                if (linkRecord != null)
                    Link = new EventRecord(linkRecord);
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
                Metadata = eventRecord.Metadata;
            }
        }
    }
}
