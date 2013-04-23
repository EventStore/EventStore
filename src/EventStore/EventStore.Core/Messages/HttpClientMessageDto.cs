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
using EventStore.Core.Data;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages
{
    public static class HttpClientMessageDto
    {
        public class ClientEventDynamic
        {
            public Guid eventId { get; set; }
            public string eventType { get; set; }

            public object data { get; set; }
            public object metadata { get; set; }
        }

        public class WriteEventsDynamic
        {
            public ClientEventDynamic[] events { get; set; }

            public WriteEventsDynamic()
            {
            }

            public WriteEventsDynamic(ClientEventDynamic[] events)
            {
                this.events = events;
            }
        }

        [XmlType(TypeName = "event")]
        public class ClientEventText
        {
            public Guid eventId { get; set; }
            public string eventType { get; set; }

            public string data { get; set; }
            public string metadata { get; set; }

            public ClientEventText()
            {
            }

            public ClientEventText(Guid eventId, string eventType, string data, string metadata)
            {
                Ensure.NotEmptyGuid(eventId, "eventId");
                Ensure.NotNull(data, "data");

                this.eventId = eventId;
                this.eventType = eventType;

                this.data = data;
                this.metadata = metadata;
            }

            public ClientEventText(Guid eventId, string eventType, byte[] data, byte[] metadata)
            {
                Ensure.NotEmptyGuid(eventId, "eventId");
                Ensure.NotNull(data, "data");

                this.eventId = eventId;
                this.eventType = eventType;

                this.data = Encoding.UTF8.GetString(data ?? LogRecord.NoData);
                this.metadata = Encoding.UTF8.GetString(metadata ?? LogRecord.NoData);
            }
        }

        [XmlRoot(ElementName = "events")]
        public class WriteEventsText
        {
            [XmlElement("event")]
            public ClientEventText[] events { get; set; }

            public WriteEventsText()
            {
            }

            public WriteEventsText(ClientEventText[] events)
            {
                Ensure.NotNull(events, "events");
                Ensure.Positive(events.Length, "events.Length");

                this.events = events;
            }
        }

        [XmlRoot(ElementName = "event")]
        public class ReadEventCompletedText
        {
            public string eventStreamId { get; set; }
            public int eventNumber { get; set; }
            public string eventType { get; set; }
            public object data { get; set; }
            public object metadata { get; set; }

            public ReadEventCompletedText()
            {
            }

            public ReadEventCompletedText(ResolvedEvent evnt)
            {
                if (evnt.Event != null)
                {
                    eventStreamId = evnt.Event.EventStreamId;
                    eventNumber = evnt.Event.EventNumber;
                    eventType = evnt.Event.EventType;

                    data = Encoding.UTF8.GetString(evnt.Event.Data ?? Empty.ByteArray);
                    metadata = Encoding.UTF8.GetString(evnt.Event.Metadata ?? Empty.ByteArray);
                }
                else
                {
                    eventStreamId = null;
                    eventNumber = EventNumber.Invalid;
                    eventType = null;
                    data = null;
                    metadata = null;
                }
            }

            public override string ToString()
            {
                return string.Format("eventStreamId: {0}, eventNumber: {1}, eventType: {2}, data: {3}, metadata: {4}",
                                     eventStreamId,
                                     eventNumber,
                                     eventType,
                                     data,
                                     metadata);
            }
        }
    }
}