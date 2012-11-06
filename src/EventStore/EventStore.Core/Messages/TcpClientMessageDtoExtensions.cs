using System;
using System.Net;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Util;

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
            private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            private static long UnixTimeStampMillisFromDateTime(DateTime timestamp)
            {
                return (long)(timestamp - UnixEpoch).TotalMilliseconds;
            }

            public EventRecord(Data.EventRecord eventRecord)
            {
                EventNumber = eventRecord.EventNumber;
                LogPosition = eventRecord.LogPosition;
                CorrelationId = eventRecord.CorrelationId.ToByteArray();
                EventId = eventRecord.EventId.ToByteArray();
                TransactionPosition = eventRecord.TransactionPosition;
                TransactionOffset = eventRecord.TransactionOffset;
                EventStreamId = eventRecord.EventStreamId;
                ExpectedVersion = eventRecord.ExpectedVersion;
                TimeStamp = UnixTimeStampMillisFromDateTime(eventRecord.TimeStamp);
                Flags = (ushort)eventRecord.Flags;
                EventType = eventRecord.EventType;
                Data = eventRecord.Data;
                Metadata = eventRecord.Metadata;
            }
        }
    }
}
