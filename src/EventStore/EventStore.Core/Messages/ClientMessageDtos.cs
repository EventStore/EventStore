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
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using ProtoBuf;

namespace EventStore.Core.Messages
{
    public static class ClientMessageDto
    {
        #region TCP DTO
        [ProtoContract]
        public class DeniedToRoute
        {
            [ProtoMember(1)]
            public string ExternalTcpAddress { get; set; }

            [ProtoMember(2)]
            public int ExternalTcpPort { get; set; }

            [ProtoMember(3)]
            public string ExternalHttpAddress { get; set; }

            [ProtoMember(4)]
            public int ExternalHttpPort { get; set; }

            public DeniedToRoute()
            {
            }

            public DeniedToRoute(IPEndPoint externalTcpEndPoint, IPEndPoint externalHttpEndPoint)
            {
                ExternalTcpAddress = externalTcpEndPoint.Address.ToString();
                ExternalTcpPort = externalTcpEndPoint.Port;

                ExternalHttpAddress = externalHttpEndPoint.Address.ToString();
                ExternalHttpPort = externalHttpEndPoint.Port;
            }
        }

        [ProtoContract]
        public class CreateStream
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2, IsRequired = false)]
            public byte[] Metadata { get; set; }

            [ProtoMember(3)]
            public bool AllowForwarding { get; set; }

            public CreateStream()
            {
            }

            public CreateStream(string eventStreamId, byte[] metadata, bool allowForwarding = true)
            {
                Ensure.NotNull(eventStreamId, "streamId");

                EventStreamId = eventStreamId;
                Metadata = metadata;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        public class CreateStreamCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ErrorCode { get; set; }

            [ProtoMember(3)]
            public string Error { get; set; }

            public CreateStreamCompleted()
            {
            }

            public CreateStreamCompleted(string eventStreamId, OperationErrorCode errorCode, string error)
            {
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
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ExpectedVersion { get; set; }

            [ProtoMember(3)]
            public Event[] Events { get; set; }

            [ProtoMember(4)]
            public bool AllowForwarding { get; set; }

            public WriteEvents()
            {
            }

            public WriteEvents(string eventStreamId, 
                               int expectedVersion, 
                               Event[] events, 
                               bool allowForwarding = true)
            {
                Ensure.NotNull(events, "events");
                Ensure.Positive(events.Length, "events.Length");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                Events = events;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        public class WriteEventsCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ErrorCode { get; set; }

            [ProtoMember(3)]
            public string Error { get; set; }

            [ProtoMember(4)]
            public int EventNumber { get; set; }

            public WriteEventsCompleted()
            {
            }

            public WriteEventsCompleted(string eventStreamId, OperationErrorCode errorCode, string error, int eventNumber)
            {
                EventStreamId = eventStreamId;
                ErrorCode = (int) errorCode;
                Error = error;
                EventNumber = eventNumber;
            }
        }

        [ProtoContract]
        public class DeleteStream
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ExpectedVersion { get; set; }

            [ProtoMember(3)]
            public bool AllowForwarding { get; set; }

            public DeleteStream()
            {
            }

            public DeleteStream(string eventStreamId, 
                                int expectedVersion, 
                                bool allowForwarding = true)
            {
                Ensure.NotNull(eventStreamId, "streamId");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        public class DeleteStreamCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ErrorCode { get; set; }

            [ProtoMember(3)]
            public string Error { get; set; }

            public DeleteStreamCompleted()
            {
            }

            public DeleteStreamCompleted(string eventStreamId, OperationErrorCode errorCode, string error)
            {
                EventStreamId = eventStreamId;
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        public class ReadEvent
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int EventNumber { get; set; }

            [ProtoMember(3)]
            public bool ResolveLinktos { get; set; }

            public ReadEvent()
            {
            }

            public ReadEvent(string eventStreamId, int eventNumber, bool resolveLinkTos)
            {
                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
                ResolveLinktos = resolveLinkTos;
            }
        }

        [ProtoContract]
        public class ReadEventCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int EventNumber { get; set; }

            [ProtoMember(3)]
            public int Result { get; set; }

            [ProtoMember(4)]
            public string EventType { get; set; }

            [ProtoMember(5)]
            public byte[] Data { get; set; }

            [ProtoMember(6)]
            public byte[] Metadata { get; set; }

            [ProtoMember(7)]
            public long LogPosition { get; set; }

            public ReadEventCompleted()
            {
            }

            public ReadEventCompleted(string eventStreamId, 
                                      int eventNumber, 
                                      SingleReadResult result,
                                      string eventType, 
                                      byte[] data, 
                                      byte[] metadata, long logPosition)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "streamId");
                Ensure.Nonnegative(eventNumber, "eventNumber");
                if (result == SingleReadResult.Success)
                    Ensure.NotNull(data, "data");

                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
                Result = (int)result;
                EventType = eventType;
                Data = data;
                Metadata = metadata;
                LogPosition = logPosition;
            }
        }

        [ProtoContract]
        public class ReadStreamEventsForward
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int StartIndex { get; set; }

            [ProtoMember(3)]
            public int MaxCount { get; set; }

            [ProtoMember(4)]
            public bool ResolveLinkTos { get; set; }

            [ProtoMember(5)]
            public bool ReturnLastEventNumber { get; set; }

            public ReadStreamEventsForward()
            {
            }

            public ReadStreamEventsForward(string eventStreamId, int startIndex, int maxCount, bool resolveLinkTos)
            {
                EventStreamId = eventStreamId;
                StartIndex = startIndex;
                MaxCount = maxCount;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        [ProtoContract]
        public class ReadStreamEventsForwardCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public EventLinkPair[] Events { get; set; }

            [ProtoMember(3)]
            public int Result { get; set; }

            [ProtoMember(4)]
            public long? LastCommitPosition { get; set; }

            [ProtoMember(5)]
            public int? LastEventNumber { get; set; }

            public ReadStreamEventsForwardCompleted()
            {
            }

            public ReadStreamEventsForwardCompleted(string eventStreamId,
                                                    EventLinkPair[] events,
                                                    RangeReadResult result,
                                                    long? lastCommitPosition,
                                                    int? lastEventNumber)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "streamId");

                EventStreamId = eventStreamId;
                Events = events;
                Result = (int)result;
                LastCommitPosition = lastCommitPosition;
                LastEventNumber = lastEventNumber;
            }
        }

        [ProtoContract]
        public class ReadStreamEventsBackward
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int StartIndex { get; set; }

            [ProtoMember(3)]
            public int MaxCount { get; set; }

            [ProtoMember(4)]
            public bool ResolveLinkTos { get; set; }

            public ReadStreamEventsBackward()
            {
            }

            public ReadStreamEventsBackward(string eventStreamId, int startIndex, int maxCount, bool resolveLinkTos)
            {
                EventStreamId = eventStreamId;
                StartIndex = startIndex;
                MaxCount = maxCount;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        [ProtoContract]
        public class ReadStreamEventsBackwardCompleted
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public EventLinkPair[] Events { get; set; }

            [ProtoMember(3)]
            public int Result { get; set; }

            [ProtoMember(4)]
            public long? LastCommitPosition { get; set; }

            public ReadStreamEventsBackwardCompleted()
            {
            }

            public ReadStreamEventsBackwardCompleted(string eventStreamId,
                                                     EventLinkPair[] events,
                                                     RangeReadResult result,
                                                     long? lastCommitPosition)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "streamId");

                EventStreamId = eventStreamId;
                Events = events;
                Result = (int)result;
                LastCommitPosition = lastCommitPosition;
            }
        }

        [ProtoContract]
        public class EventLinkPair
        {
            [ProtoMember(1)]
            public EventRecord Event { get; set; }

            [ProtoMember(2)]
            public EventRecord Link { get; set; }

            public EventLinkPair()
            {
            }

            public EventLinkPair(Data.EventRecord @event, Data.EventRecord link)
            {
                Ensure.NotNull(@event, "event");
                Event = new EventRecord(@event);
                Link = link == null ? null : new EventRecord(link);
            }
        }

        [ProtoContract]
        public class EventRecord
        {
            [ProtoMember(1)]
            public readonly int EventNumber;

            [ProtoMember(2)]
            public readonly long LogPosition;

            [ProtoMember(3)]
            public readonly byte[] CorrelationId;

            [ProtoMember(4)]
            public readonly byte[] EventId;

            [ProtoMember(5)]
            public readonly long TransactionPosition;

            [ProtoMember(6)]
            public readonly int TransactionOffset;

            [ProtoMember(7)]
            public readonly string EventStreamId;

            [ProtoMember(8)]
            public readonly int ExpectedVersion;

            [ProtoMember(9)]
            public readonly DateTime TimeStamp;

            [ProtoMember(10)]
            public readonly ushort Flags;

            [ProtoMember(11)]
            public readonly string EventType;

            [ProtoMember(12)]
            public readonly byte[] Data;

            [ProtoMember(13)]
            public readonly byte[] Metadata;

            public EventRecord()
            {
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
                TimeStamp = eventRecord.TimeStamp;
                Flags = (ushort)eventRecord.Flags;
                EventType = eventRecord.EventType;
                Data = eventRecord.Data;
                Metadata = eventRecord.Metadata;
            }
        }

        [ProtoContract]
        public class ReadAllEventsForward
        {
            [ProtoMember(1)]
            public long CommitPosition { get; set; }

            [ProtoMember(2)]
            public long PreparePosition { get; set; }

            [ProtoMember(3)]
            public int MaxCount { get; set; }

            [ProtoMember(4)]
            public bool ResolveLinktos { get; set; }

            public ReadAllEventsForward()
            {
            }

            public ReadAllEventsForward(long commitPosition, long preparePosition, int maxCount, bool resolveLinktos)
            {
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                MaxCount = maxCount;
                ResolveLinktos = resolveLinktos;
            }
        }

        [ProtoContract]
        public class ReadAllEventsForwardCompleted
        {
            [ProtoMember(1)]
            public long CommitPosition { get; set; }

            [ProtoMember(2)]
            public long PreparePosition { get; set; }

            [ProtoMember(3)]
            public EventLinkPair[] Events { get; set; }

            [ProtoMember(4)]
            public long NextCommitPosition { get; set; }

            [ProtoMember(5)]
            public long NextPreparePosition { get; set; }

            public ReadAllEventsForwardCompleted()
            {
            }

            public ReadAllEventsForwardCompleted(long commitPosition,
                                                 long preparePosition,
                                                 EventLinkPair[] events,
                                                 long nextCommitPosition,
                                                 long nextPreparePosition)
            {
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                Events = events;
                NextCommitPosition = nextCommitPosition;
                NextPreparePosition = nextPreparePosition;
            }
        }

        [ProtoContract]
        public class ReadAllEventsBackward
        {
            [ProtoMember(1)]
            public long CommitPosition { get; set; }

            [ProtoMember(2)]
            public long PreparePosition { get; set; }

            [ProtoMember(3)]
            public int MaxCount { get; set; }

            [ProtoMember(4)]
            public bool ResolveLinkTos { get; set; }

            public ReadAllEventsBackward()
            {
            }

            public ReadAllEventsBackward(long commitPosition, long preparePosition, int maxCount, bool resolveLinktos)
            {
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                MaxCount = maxCount;
                ResolveLinkTos = resolveLinktos;
            }
        }

        [ProtoContract]
        public class ReadAllEventsBackwardCompleted
        {
            [ProtoMember(1)]
            public long CommitPosition { get; set; }

            [ProtoMember(2)]
            public long PreparePosition { get; set; }

            [ProtoMember(3)]
            public EventLinkPair[] Events { get; set; }

            [ProtoMember(4)]
            public long NextCommitPosition { get; set; }

            [ProtoMember(5)]
            public long NextPreparePosition { get; set; }

            public ReadAllEventsBackwardCompleted()
            {
            }

            public ReadAllEventsBackwardCompleted(long commitPosition,
                                                  long preparePosition,
                                                  EventLinkPair[] events,
                                                  long nextCommitPosition,
                                                  long nextPreparePosition)
            {
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                Events = events;
                NextCommitPosition = nextCommitPosition;
                NextPreparePosition = nextPreparePosition;
            }
        }

        [ProtoContract]
        public class TransactionStart
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int ExpectedVersion { get; set; }

            [ProtoMember(3)]
            public bool AllowForwarding { get; set; }

            public TransactionStart()
            {
            }

            public TransactionStart(string eventStreamId, 
                                    int expectedVersion,
                                    bool allowForwarding = true)
            {
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        public class TransactionStartCompleted
        {
            [ProtoMember(1)]
            public long TransactionId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int ErrorCode { get; set; }

            [ProtoMember(4)]
            public string Error { get; set; }

            public TransactionStartCompleted()
            {
            }

            public TransactionStartCompleted(long transactionId,
                                             string eventStreamId,
                                             OperationErrorCode errorCode,
                                             string error)
            {
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                ErrorCode = (int) errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        public class TransactionWrite
        {
            [ProtoMember(1)]
            public long TransactionId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public Event[] Events { get; set; }

            [ProtoMember(4)]
            public bool AllowForwarding { get; set; }

            public TransactionWrite()
            {
            }

            public TransactionWrite(long transactionId, 
                                    string eventStreamId, 
                                    Event[] events,
                                    bool allowForwarding = true)
            {
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                Events = events;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        public class TransactionWriteCompleted
        {
            [ProtoMember(1)]
            public long TransactionId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public int ErrorCode { get; set; }

            [ProtoMember(4)]
            public string Error { get; set; }

            public TransactionWriteCompleted()
            {
            }

            public TransactionWriteCompleted(long transactionId, string eventStreamId, OperationErrorCode errorCode, string error)
            {
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                ErrorCode = (int) errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        public class TransactionCommit
        {
            [ProtoMember(1)]
            public long TransactionId { get; set; }

            [ProtoMember(2)]
            public string EventStreamId { get; set; }

            [ProtoMember(3)]
            public bool AllowForwarding { get; set; }

            public TransactionCommit()
            {
            }

            public TransactionCommit(long transactionId, 
                                     string eventStreamId, 
                                     bool allowForwarding = true)
            {
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                AllowForwarding = allowForwarding;
            }
        }

        [ProtoContract]
        public class TransactionCommitCompleted
        {
            [ProtoMember(1)]
            public long TransactionId;

            [ProtoMember(2)]
            public int ErrorCode;

            [ProtoMember(3)]
            public string Error;

            public TransactionCommitCompleted()
            {
            }

            public TransactionCommitCompleted(long transactionId, OperationErrorCode errorCode, string error)
            {
                TransactionId = transactionId;
                ErrorCode = (int) errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        public class SubscribeToStream
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            public SubscribeToStream()
            {
            }

            public SubscribeToStream(string eventStreamId)
            {
                EventStreamId = eventStreamId;
            }
        }

        [ProtoContract]
        public class UnsubscribeFromStream
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            public UnsubscribeFromStream()
            {
            }

            public UnsubscribeFromStream(string eventStreamId)
            {
                EventStreamId = eventStreamId;
            }
        }

        [ProtoContract]
        public class SubscribeToAllStreams
        {
        }

        [ProtoContract]
        public class UnsubscribeFromAllStreams
        {
        }

        [ProtoContract]
        public class StreamEventAppeared
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            [ProtoMember(2)]
            public int EventNumber { get; set; }

            [ProtoMember(3)]
            public byte[] EventId { get; set; }

            [ProtoMember(4)]
            public string EventType { get; set; }

            [ProtoMember(5)]
            public byte[] Data { get; set; }

            [ProtoMember(6)]
            public byte[] Metadata { get; set; }

            public StreamEventAppeared()
            {
            }

            public StreamEventAppeared(int eventNumber, PrepareLogRecord @event)
            {
                EventStreamId = @event.EventStreamId;
                EventNumber = eventNumber;
                EventId = @event.EventId.ToByteArray();
                EventType = @event.EventType;
                Data = @event.Data;
                Metadata = @event.Metadata;
            }
        }

        [ProtoContract]
        public class SubscriptionDropped
        {
            [ProtoMember(1)]
            public string EventStreamId { get; set; }

            public SubscriptionDropped()
            {
            }

            public SubscriptionDropped(string eventStreamId)
            {
                EventStreamId = eventStreamId;
            }
        }

        [ProtoContract]
        public class SubscriptionToAllDropped
        {
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
