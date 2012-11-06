using System;
using System.Runtime.Serialization;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages
{
    public static class HttpClientMessageDto
    {
        #region HTTP DTO

        [DataContract(Name = "event", Namespace = "")]
        public class ClientEventText
        {
            [DataMember]
            public Guid EventId { get; set; }
            [DataMember]
            public string EventType { get; set; }

            [DataMember]
            public object Data { get; set; }
            [DataMember]
            public object Metadata { get; set; }

            public ClientEventText()
            {
            }

            public ClientEventText(Guid eventId, string eventType, object data, object metadata)
            {
                Ensure.NotEmptyGuid(eventId, "eventId");
                Ensure.NotNull(data, "data");

                EventId = eventId;
                EventType = eventType;

                Data = data;
                Metadata = metadata;
            }

            public ClientEventText(Guid eventId, string eventType, byte[] data, byte[] metaData)
            {
                Ensure.NotEmptyGuid(eventId, "eventId");
                Ensure.NotNull(data, "data");

                EventId = eventId;
                EventType = eventType;

                Data = Encoding.UTF8.GetString(data ?? LogRecord.NoData);
                Metadata = Encoding.UTF8.GetString(metaData ?? LogRecord.NoData);
            }
        }

        [DataContract(Name = "write-event", Namespace = "")]
        public class WriteEventText
        {
            [DataMember]
            public int ExpectedVersion { get; set; }

            [DataMember]
            public ClientEventText[] Events { get; set; }

            public WriteEventText()
            {
            }

            public WriteEventText(int expectedVersion, ClientEventText[] events)
            {
                Ensure.NotNull(events, "events");
                Ensure.Positive(events.Length, "events.Length");

                ExpectedVersion = expectedVersion;
                Events = events;
            }
        }

        [DataContract(Name = "read-event-result", Namespace = "")]
        public class ReadEventCompletedText
        {
            [DataMember]
            public string EventStreamId { get; set; }
            [DataMember]
            public int EventNumber { get; set; }

            [DataMember]
            public string EventType { get; set; }

            [DataMember]
            public object Data { get; set; }
            [DataMember]
            public object Metadata { get; set; }

            public ReadEventCompletedText()
            {
            }

            public ReadEventCompletedText(ClientMessage.ReadEventCompleted message)
            {
                if (message.Record != null)
                {
                    EventStreamId = message.Record.EventStreamId;
                    EventNumber = message.Record.EventNumber;
                    EventType = message.Record.EventType;

                    Data = Encoding.UTF8.GetString(message.Record.Data ?? new byte[0]);
                    Metadata = Encoding.UTF8.GetString(message.Record.Metadata ?? new byte[0]);
                }
                else
                {
                    EventStreamId = null;
                    EventNumber = Core.Data.EventNumber.Invalid;
                    EventType = null;
                    Data = null;
                    Metadata = null;
                }
            }

            public override string ToString()
            {
                return string.Format("EventStreamId: {0}, EventNumber: {1}, EventType: {2}, Data: {3}, Metadata: {4}",
                                     EventStreamId,
                                     EventNumber,
                                     EventType,
                                     Data,
                                     Metadata);
            }
        }

        [DataContract(Name = "create-stream", Namespace = "")]
        public class CreateStreamText
        {
            [DataMember]
            public string EventStreamId { get; set; }
            [DataMember]
            public string Metadata { get; set; }

            public CreateStreamText()
            {
            }

            public CreateStreamText(string eventStreamId, string metadata)
            {
                EventStreamId = eventStreamId;
                Metadata = metadata;
            }
        }

        [DataContract(Name = "delete-stream", Namespace = "")]
        public class DeleteStreamText
        {
            [DataMember]
            public int ExpectedVersion { get; set; }

            public DeleteStreamText()
            {
            }

            public DeleteStreamText(int expectedVersion)
            {
                ExpectedVersion = expectedVersion;
            }
        }

        #endregion
    }
}