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
using System.Runtime.Serialization;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using ProtoBuf;

namespace EventStore.Core.Messages
{
    public static class ClientMessageDto
    {
        #region TCP DTO
        [ProtoContract]
        public class CreateStream
        {
            [ProtoMember(1, IsRequired = false)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3, IsRequired = false)]
            public byte[] Metadata { get; set; }

            public CreateStream()
            {
            }

            public CreateStream(Guid correlationId,
                                string eventStreamId,
                                byte[] metadata)
            {
                Ensure.NotNull(eventStreamId, "eventStreamId");

                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                Metadata = metadata;
            }
        }

        [ProtoContract]
        public class CreateStreamCompleted
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int ErrorCode { get; set; }

            [ProtoMember(4)]
            public string Error { get; set; }

            public CreateStreamCompleted()
            {
            }

            public CreateStreamCompleted(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        public class Event
        {
            [ProtoMember(1)]
            public byte[] EventId { get; set; }

            [ProtoMember(2, IsRequired = false)]
            public string EventType { get; set; }

            [ProtoMember(3)]
            public byte[] Data { get; set; }

            [ProtoMember(4, IsRequired = false)]
            public byte[] Metadata { get; set; }

            public Event()
            {
            }

            public Event(Guid eventId, string eventType, byte[] data, byte[] metadata)
            {
                EventId = eventId.ToByteArray();
                EventType = eventType;
                Data = data;
                Metadata = metadata;
            }

            public Event(Data.Event evnt)
            {
                EventId = evnt.EventId.ToByteArray();
                EventType = evnt.EventType;
                Data = evnt.Data;
                Metadata = evnt.Metadata;
            }
        }

        [ProtoContract]
        public class WriteEvents
        {
            [ProtoMember(1, IsRequired = false)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int ExpectedVersion { get; set; }

            [ProtoMember(4)]
            public Event[] Events { get; set; }

            public WriteEvents()
            {
            }

            public WriteEvents(Guid correlationId, string eventStreamId, int expectedVersion, Event[] events)
            {
                Ensure.NotNull(events, "events");
                Ensure.Positive(events.Length, "events.Length");

                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                Events = events;
            }
        }

        [ProtoContract]
        public class WriteEventsCompleted
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int ErrorCode { get; set; }

            [ProtoMember(4)]
            public string Error { get; set; }

            [ProtoMember(5)]
            public int EventNumber { get; set; }

            public WriteEventsCompleted()
            {
            }

            public WriteEventsCompleted(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error, int eventNumber)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                ErrorCode = (int) errorCode;
                Error = error;
                EventNumber = eventNumber;
            }
        }

        [ProtoContract]
        public class DeleteStream
        {
            [ProtoMember(1, IsRequired = false)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int ExpectedVersion { get; set; }

            public DeleteStream()
            {
            }

            public DeleteStream(Guid correlationId, string eventStreamId, int expectedVersion)
            {
                Ensure.NotNull(eventStreamId, "eventStreamId");

                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        [ProtoContract]
        public class DeleteStreamCompleted
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int ErrorCode { get; set; }

            [ProtoMember(4)]
            public string Error { get; set; }

            public DeleteStreamCompleted()
            {
            }

            public DeleteStreamCompleted(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        public class ReadEvent
        {
            [ProtoMember(1, IsRequired = false)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int EventNumber { get; set; }

            [ProtoMember(4)]
            public bool ResolveLinktos { get; set; }

            public ReadEvent()
            {
            }

            public ReadEvent(Guid correlationId, string eventStreamId, int eventNumber)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
            }
        }

        [ProtoContract]
        public class ReadEventCompleted
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int EventNumber { get; set; }

            [ProtoMember(4)]
            public int Result { get; set; }

            [ProtoMember(5)]
            public string EventType { get; set; }

            [ProtoMember(6)]
            public byte[] Data { get; set; }

            [ProtoMember(7)]
            public byte[] Metadata { get; set; }

            public ReadEventCompleted()
            {
            }

            public ReadEventCompleted(Guid correlationId, 
                                      string eventStreamId, 
                                      int eventNumber, 
                                      SingleReadResult result,
                                      string eventType, 
                                      byte[] data, 
                                      byte[] metadata)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                Ensure.Nonnegative(eventNumber, "eventNumber");
                if (result == SingleReadResult.Success)
                    Ensure.NotNull(data, "data");

                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
                Result = (int)result;
                EventType = eventType;
                Data = data;
                Metadata = metadata;
            }
        }

        [ProtoContract]
        public class ReadStreamEventsForward
        {
            [ProtoMember(1, IsRequired = false)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int StartIndex { get; set; }

            [ProtoMember(4)]
            public int MaxCount { get; set; }

            [ProtoMember(5)]
            public bool ResolveLinktos { get; set; }

            public ReadStreamEventsForward()
            {
            }

            public ReadStreamEventsForward(Guid correlationId, string eventStreamId, int startIndex, int maxCount)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                StartIndex = startIndex;
                MaxCount = maxCount;
            }
        }

        [ProtoContract]
        public class ReadStreamEventsForwardCompleted
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public EventRecord[] Events { get; set; }

            [ProtoMember(4)]
            public EventRecord[] LinkToEvents { get; set; }

            [ProtoMember(5)]
            public int Result { get; set; }

            [ProtoMember(6)]
            public long? LastCommitPosition { get; set; }

            public ReadStreamEventsForwardCompleted()
            {
            }

            public ReadStreamEventsForwardCompleted(Guid correlationId,
                                                    string eventStreamId,
                                                    EventRecord[] events,
                                                    EventRecord[] linkToEvents,
                                                    RangeReadResult result,
                                                    long? lastCommitPosition)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");

                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                Events = events;
                LinkToEvents = linkToEvents;
                Result = (int)result;
                LastCommitPosition = lastCommitPosition;
            }
        }

        [ProtoContract]
        public class ReadStreamEventsBackward
        {
            [ProtoMember(1, IsRequired = false)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int StartIndex { get; set; }

            [ProtoMember(4)]
            public int MaxCount { get; set; }

            [ProtoMember(5)]
            public bool ResolveLinktos { get; set; }

            public ReadStreamEventsBackward()
            {
            }

            public ReadStreamEventsBackward(Guid correlationId, string eventStreamId, int startIndex, int maxCount)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                StartIndex = startIndex;
                MaxCount = maxCount;
            }
        }

        [ProtoContract]
        public class ReadStreamEventsBackwardCompleted
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public EventRecord[] Events { get; set; }

            [ProtoMember(4)]
            public EventRecord[] LinkToEvents { get; set; }

            [ProtoMember(5)]
            public int Result { get; set; }

            [ProtoMember(6)]
            public long? LastCommitPosition { get; set; }

            public ReadStreamEventsBackwardCompleted()
            {
            }

            public ReadStreamEventsBackwardCompleted(Guid correlationId,
                                                      string eventStreamId,
                                                      EventRecord[] events,
                                                      EventRecord[] linkToEvents,
                                                      RangeReadResult result,
                                                      long? lastCommitPosition)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");

                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                Events = events;
                LinkToEvents = linkToEvents;
                Result = (int)result;
                LastCommitPosition = lastCommitPosition;
            }
        }

        [ProtoContract]
        public class TransactionStart
        {
            [ProtoMember(1, IsRequired = false)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int ExpectedVersion { get; set; }

            public TransactionStart()
            {
            }

            public TransactionStart(Guid correlationId, string eventStreamId, int expectedVersion)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        [ProtoContract]
        public class TransactionStartCompleted
        {
            [ProtoMember(1, IsRequired = false)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public long TransactionId { get; set; }

            [ProtoMember(3)]
            public string EventStreamId { get; set; }

            [ProtoMember(4)]
            public int ErrorCode { get; set; }

            [ProtoMember(5)]
            public string Error { get; set; }

            public TransactionStartCompleted()
            {
            }

            public TransactionStartCompleted(Guid correlationId,
                                             long transactionId,
                                             string eventStreamId,
                                             OperationErrorCode errorCode,
                                             string error)
            {
                CorrelationId = correlationId.ToByteArray();
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                ErrorCode = (int) errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        public class TransactionWrite
        {
            [ProtoMember(1, IsRequired = false)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public long TransactionId { get; set; }

            [ProtoMember(3)]
            public string EventStreamId { get; set; }

            [ProtoMember(4)]
            public Event[] Events { get; set; }

            public TransactionWrite()
            {
            }

            public TransactionWrite(Guid correlationId, long transactionId, string eventStreamId, Event[] events)
            {
                CorrelationId = correlationId.ToByteArray();
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                Events = events;
            }
        }

        [ProtoContract]
        public class TransactionWriteCompleted
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public long TransactionId { get; set; }

            [ProtoMember(3)]
            public string EventStreamId { get; set; }

            [ProtoMember(4)]
            public int ErrorCode { get; set; }

            [ProtoMember(5)]
            public string Error { get; set; }

            public TransactionWriteCompleted()
            {
            }

            public TransactionWriteCompleted(Guid correlationId, long transactionId, string eventStreamId, OperationErrorCode errorCode, string error)
            {
                TransactionId = transactionId;
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                ErrorCode = (int) errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        public class TransactionCommit
        {
            [ProtoMember(1, IsRequired = false)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public long TransactionId { get; set; }

            [ProtoMember(3)]
            public string EventStreamId { get; set; }

            public TransactionCommit()
            {
            }

            public TransactionCommit(Guid correlationId, long transactionId, string eventStreamId)
            {
                CorrelationId = correlationId.ToByteArray();
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
            }
        }

        [ProtoContract]
        public class TransactionCommitCompleted
        {
            [ProtoMember(1)]
            public byte[] CorrelationId;

            [ProtoMember(2)]
            public long TransactionId;

            [ProtoMember(3)]
            public int ErrorCode;

            [ProtoMember(4)]
            public string Error;

            public TransactionCommitCompleted()
            {
            }

            public TransactionCommitCompleted(Guid correlationId, long transactionId, OperationErrorCode errorCode, string error)
            {
                CorrelationId = correlationId.ToByteArray();
                TransactionId = transactionId;
                ErrorCode = (int) errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        public class SubscribeToStream
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }
            
            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            public SubscribeToStream()
            {
            }

            public SubscribeToStream(Guid correlationId, string eventStreamId)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
            }
        }

        [ProtoContract]
        public class UnsubscribeFromStream
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            public UnsubscribeFromStream()
            {
            }

            public UnsubscribeFromStream(Guid correlationId, string eventStreamId)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
            }
        }

        [ProtoContract]
        public class SubscribeToAllStreams
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            public SubscribeToAllStreams()
            {
            }

            public SubscribeToAllStreams(Guid correlationId)
            {
                CorrelationId = correlationId.ToByteArray();
            }
        }

        [ProtoContract]
        public class UnsubscribeFromAllStreams
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            public UnsubscribeFromAllStreams()
            {
            }

            public UnsubscribeFromAllStreams(Guid correlationId)
            {
                CorrelationId = correlationId.ToByteArray();
            }
        }

        [ProtoContract]
        public class StreamEventAppeared
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int EventNumber { get; set; }

            [ProtoMember(4)]
            public Guid EventId { get; set; }

            [ProtoMember(5)]
            public string EventType { get; set; }

            [ProtoMember(6)]
            public byte[] Data { get; set; }

            [ProtoMember(7)]
            public byte[] Metadata { get; set; }

            public StreamEventAppeared()
            {
            }

            public StreamEventAppeared(Guid correlationId, int eventNumber, PrepareLogRecord @event)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = @event.EventStreamId;
                EventNumber = eventNumber;
                EventId = @event.EventId;
                EventType = @event.EventType;
                Data = @event.Data;
                Metadata = @event.Metadata;
            }
        }

        [ProtoContract]
        public class SubscriptionDropped
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            public SubscriptionDropped()
            {
            }

            public SubscriptionDropped(Guid correlationId, string eventStreamId)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
            }
        }

        [ProtoContract]
        public class SubscriptionToAllDropped
        {
            [ProtoMember(1)]
            public byte[] CorrelationId { get; set; }

            public SubscriptionToAllDropped()
            {
            }

            public SubscriptionToAllDropped(Guid correlationId)
            {
                CorrelationId = correlationId.ToByteArray();
            }
        }
        #endregion

        #region HTTP DTO

        [DataContract(Name = "event", Namespace = "")]
        public class EventText
        {
            [DataMember]
            public Guid EventId { get; set; }
            [DataMember]
            public string EventType { get; set; }

            [DataMember]
            public object Data { get; set; }
            [DataMember]
            public object Metadata { get; set; }

            public EventText()
            {
            }

            public EventText(Guid eventId, string eventType, object data, object metadata)
            {
                Ensure.NotEmptyGuid(eventId, "eventId");
                Ensure.NotNull(data, "data");

                EventId = eventId;
                EventType = eventType;

                Data = data;
                Metadata = metadata;
            }

            public EventText(Guid eventId, string eventType, byte[] data, byte[] metaData)
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
            public Guid CorrelationId { get; set; }
            [DataMember]
            public int ExpectedVersion { get; set; }

            [DataMember]
            public EventText[] Events { get; set; }

            public WriteEventText()
            {
            }

            public WriteEventText(Guid correlationId, int expectedVersion, EventText[] events)
            {
                Ensure.NotNull(events, "events");
                Ensure.Positive(events.Length, "events.Length");

                CorrelationId = correlationId;
                ExpectedVersion = expectedVersion;
                Events = events;
            }
        }

        public class WriteEventCompletedText
        {
            public Guid CorrelationId { get; set; }
            public string EventStreamId { get; set; }
            public OperationErrorCode ErrorCode { get; set; }
            public string Error { get; set; }
            public int EventNumber { get; set; }

            public WriteEventCompletedText()
            {
            }

            public WriteEventCompletedText(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error, int eventNumber)
            {
                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                ErrorCode = errorCode;
                Error = error;
                EventNumber = eventNumber;
            }

            public WriteEventCompletedText(ClientMessage.WriteEventsCompleted message)
            {
                CorrelationId = message.CorrelationId;
                ErrorCode = message.ErrorCode;
                Error = message.Error;
            }
        }

        [DataContract(Name = "read-event-result", Namespace = "")]
        public class ReadEventCompletedText
        {
            [DataMember]
            public Guid CorrelationId { get; set; }

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
                CorrelationId = message.Record.CorrelationId;

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
                return string.Format("CorrelationId: {0}, EventStreamId: {1}, EventNumber: {2}, EventType: {3}, Data: {4}, Metadata: {5}", 
                                     CorrelationId, 
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
            public Guid CorrelationId { get; set; }
            [DataMember]
            public string EventStreamId { get; set; }
            [DataMember]
            public string Metadata { get; set; }

            public CreateStreamText()
            {
            }

            public CreateStreamText(Guid correlationId, string eventStreamId, string metadata)
            {
                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                Metadata = metadata;
            }
        }

        public class CreateStreamCompletedText
        {
            public Guid CorrelationId { get; set; }
            public string EventStreamId { get; set; }

            public int ErrorCode { get; set; }
            public string Error { get; set; }

            public CreateStreamCompletedText()
            {
            }

            public CreateStreamCompletedText(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error)
            {
                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [DataContract(Name = "delete-stream", Namespace = "")]
        public class DeleteStreamText
        {
            [DataMember]
            public Guid CorrelationId { get; set; }
            [DataMember]
            public int ExpectedVersion { get; set; }

            public DeleteStreamText()
            {
            }

            public DeleteStreamText(Guid correlationId, int expectedVersion)
            {
                CorrelationId = correlationId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class DeleteStreamCompletedText
        {
            public Guid CorrelationId { get; set; }
            public string EventStreamId { get; set; }

            public int ErrorCode { get; set; }
            public string Error { get; set; }

            public DeleteStreamCompletedText()
            {
            }

            public DeleteStreamCompletedText(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error)
            {
                CorrelationId = correlationId;
                EventStreamId = eventStreamId;

                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        #endregion
    }
}
