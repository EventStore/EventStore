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
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.System;
using ProtoBuf;

namespace EventStore.ClientAPI.Transport.Tcp
{
    internal static class ClientMessages
    {
        [ProtoContract]
        internal class CreateStream
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
        internal class CreateStreamCompleted
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
        internal class Event
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

            public Event(System.Event evnt)
            {
                EventId = evnt.EventId.ToByteArray();
                EventType = evnt.EventType;
                Data = evnt.Data;
                Metadata = evnt.Metadata;
            }
        }

        [ProtoContract]
        internal class WriteEvents
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
        internal class WriteEventsCompleted
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
                ErrorCode = (int)errorCode;
                Error = error;
                EventNumber = eventNumber;
            }
        }

        [ProtoContract]
        internal class DeleteStream
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
        internal class DeleteStreamCompleted
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
        internal class ReadEvent
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
        internal class ReadEventCompleted
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
        internal class ReadStreamEventsForward
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

            public ReadStreamEventsForward(Guid correlationId, string eventStreamId, int startIndex, int maxCount, bool resolveLinkTos)
            {
                CorrelationId = correlationId.ToByteArray();
                EventStreamId = eventStreamId;
                StartIndex = startIndex;
                MaxCount = maxCount;
                ResolveLinktos = resolveLinkTos;
            }
        }

        [ProtoContract]
        internal class ReadStreamEventsForwardCompleted
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
        internal class TransactionStart
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
        internal class TransactionStartCompleted
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
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        internal class TransactionWrite
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
        internal class TransactionWriteCompleted
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
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        internal class TransactionCommit
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
        internal class TransactionCommitCompleted
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
                ErrorCode = (int)errorCode;
                Error = error;
            }
        }

        [ProtoContract]
        internal class SubscribeToStream
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
        internal class UnsubscribeFromStream
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
        internal class SubscribeToAllStreams
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
        internal class UnsubscribeFromAllStreams
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
        internal class StreamEventAppeared
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
        }

        [ProtoContract]
        internal class SubscriptionDropped
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
        internal class SubscriptionToAllDropped
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
    }
}