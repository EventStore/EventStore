// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Text;
using System.Xml.Serialization;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages
{
    public static class HttpClientMessageDto
    {
        #region HTTP DTO

        public class ClientEventDynamic
        {
            public Guid EventId { get; set; }
            public string EventType { get; set; }

            public object Data { get; set; }
            public object Metadata { get; set; }
        }

        public class WriteEventsDynamic
        {
            public int ExpectedVersion { get; set; }
            public ClientEventDynamic[] Events { get; set; }

            public WriteEventsDynamic()
            {
            }

            public WriteEventsDynamic(int expectedVersion, ClientEventDynamic[] events)
            {
                ExpectedVersion = expectedVersion;
                Events = events;
            }
        }

        [XmlType(TypeName = "event")]
        public class ClientEventText
        {
            public Guid EventId { get; set; }
            public string EventType { get; set; }

            public string Data { get; set; }
            public string Metadata { get; set; }

            public ClientEventText()
            {
            }

            public ClientEventText(Guid eventId, string eventType, string data, string metadata)
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

        [XmlRoot(ElementName = "write-events")]
        public class WriteEventsText
        {
            public int ExpectedVersion { get; set; }
            public ClientEventText[] Events { get; set; }

            public WriteEventsText()
            {
            }

            public WriteEventsText(int expectedVersion, ClientEventText[] events)
            {
                Ensure.NotNull(events, "events");
                Ensure.Positive(events.Length, "events.Length");

                ExpectedVersion = expectedVersion;
                Events = events;
            }
        }

        [XmlRoot(ElementName = "read-event-result")]
        public class ReadEventCompletedText
        {
            public string EventStreamId { get; set; }
            public int EventNumber { get; set; }
            public string EventType { get; set; }
            public object Data { get; set; }
            public object Metadata { get; set; }

            public ReadEventCompletedText()
            {
            }

            public ReadEventCompletedText(ClientMessage.ReadEventCompleted message)
            {
                if (message.Record.Event != null)
                {
                    EventStreamId = message.Record.Event.EventStreamId;
                    EventNumber = message.Record.Event.EventNumber;
                    EventType = message.Record.Event.EventType;

                    Data = Encoding.UTF8.GetString(message.Record.Event.Data ?? new byte[0]);
                    Metadata = Encoding.UTF8.GetString(message.Record.Event.Metadata ?? new byte[0]);
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
        #endregion
    }
}