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
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages
{
    public enum OperationErrorCode
    {
        Success = 0,
        PrepareTimeout,
        CommitTimeout,
        ForwardTimeout,
        WrongExpectedVersion,
        StreamDeleted,
        InvalidTransaction
    }

    public static class ClientMessage
    {
        public class RequestShutdown: Message
        {
        }

        public abstract class WriteMessage : Message
        {
        }

        public abstract class ReadMessage: Message
        {
        }

        public class ForwardMessage: Message
        {
            public readonly Message Message;

            public ForwardMessage(Message message)
            {
                Message = message;
            }
        }

        public class CreateStream: WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;

            public readonly string EventStreamId;
            public readonly byte[] Metadata;

            public CreateStream(Guid correlationId, IEnvelope envelope, string eventStreamId, byte[] metadata)
            {
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");
                
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                Metadata = metadata;
            }
        }

        public class CreateStreamCompleted : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly OperationErrorCode ErrorCode;
            public readonly string Error;

            public CreateStreamCompleted(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error)
            {
                Ensure.NotNull(eventStreamId, "eventStreamId");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                ErrorCode = errorCode;
                Error = error;
            }
        }

        public class WriteEvents : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;

            public readonly string EventStreamId;
            public readonly int ExpectedVersion;
            public readonly Event[] Events;

            public WriteEvents(Guid correlationId, IEnvelope envelope, string eventStreamId, int expectedVersion, Event[] events)
            {
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");
                Ensure.NotNull(events, "events");
                Ensure.Positive(events.Length, "events.Length");

                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                Events = events;
            }

            public WriteEvents(Guid correlationId, IEnvelope envelope, string eventStreamId, int expectedVersion, Event @event)
                : this(correlationId, envelope, eventStreamId, expectedVersion, new[]{@event})
            {
            }

            public override string ToString()
            {
                return string.Format("WRITE: CorrelationId: {0}, EventStreamId: {1}, ExpectedVersion: {2}, Events: {3}", CorrelationId, EventStreamId, ExpectedVersion, Events.Length);
            }
        }

        public class WriteEventsCompleted : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly OperationErrorCode ErrorCode;
            public readonly string Error;
            public readonly int EventNumber;

            public WriteEventsCompleted(Guid correlationId, string eventStreamId, int eventNumber)
            {
                Ensure.NotNull(eventStreamId, "eventStreamId");
                Ensure.Nonnegative(eventNumber, "eventNumber");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                ErrorCode = OperationErrorCode.Success;
                Error = null;
                EventNumber = eventNumber;
            }

            public WriteEventsCompleted(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (errorCode == OperationErrorCode.Success)
                    throw new ArgumentException("Invalid constructor used for successful write.", "errorCode");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                ErrorCode = errorCode;
                Error = error;
                EventNumber = Data.EventNumber.Invalid;
            }
        }

        public class TransactionStart : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly string EventStreamId;
            public readonly int ExpectedVersion;

            public TransactionStart(Guid correlationId, IEnvelope envelope, string eventStreamId, int expectedVersion)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class TransactionStartCompleted : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly long TransactionId;
            public readonly string EventStreamId;
            public readonly OperationErrorCode ErrorCode;
            public readonly string Error;

            public TransactionStartCompleted(Guid correlationId, 
                                             long transactionId, 
                                             string eventStreamId, 
                                             OperationErrorCode errorCode, 
                                             string error)
            {
                CorrelationId = correlationId;
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                ErrorCode = errorCode;
                Error = error;
            }
        }

        public class TransactionWrite : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long TransactionId;
            public readonly string EventStreamId;
            public readonly Event[] Events;

            public TransactionWrite(Guid correlationId, IEnvelope envelope, long transactionId, string eventStreamId, Event[] events)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                Events = events;
            }
        }

        public class TransactionWriteCompleted : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly long TransactionId;
            public readonly string EventStreamId;
            public readonly OperationErrorCode ErrorCode;
            public readonly string Error;

            public TransactionWriteCompleted(Guid correlationId, 
                                             long transactionId, 
                                             string eventStreamId, 
                                             OperationErrorCode errorCode, 
                                             string error)
            {
                CorrelationId = correlationId;
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                ErrorCode = errorCode;
                Error = error;
            }
        }

        public class TransactionCommit : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long TransactionId;
            public readonly string EventStreamId;

            public TransactionCommit(Guid correlationId, IEnvelope envelope, long transactionId, string eventStreamId)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
            }
        }

        public class TransactionCommitCompleted : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly long TransactionId;
            public readonly OperationErrorCode ErrorCode;
            public readonly string Error;

            public TransactionCommitCompleted(Guid correlationId, 
                                              long transactionId, 
                                              OperationErrorCode errorCode, 
                                              string error)
            {
                CorrelationId = correlationId;
                TransactionId = transactionId;
                ErrorCode = errorCode;
                Error = error;
            }
        }

        public class DeleteStream : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly string EventStreamId;
            public readonly int ExpectedVersion;

            public DeleteStream(Guid correlationId,
                                IEnvelope envelope,
                                string eventStreamId,
                                int expectedVersion)
            {
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");

                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class DeleteStreamCompleted : WriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly OperationErrorCode ErrorCode;
            public readonly string Error;

            public DeleteStreamCompleted(Guid correlationId, string eventStreamId, OperationErrorCode errorCode, string error)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                ErrorCode = errorCode;
                Error = error;
            }
        }

        public class ReadEvent : ReadMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;

            public readonly string EventStreamId;
            public readonly int EventNumber;
            public readonly bool ResolveLinkTos;

            public ReadEvent(Guid correlationId, IEnvelope envelope, string eventStreamId, int eventNumber, bool resolveLinkTos)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        public class ReadEventCompleted : ReadMessage
        {
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly int EventNumber;
            public readonly SingleReadResult Result;
            public readonly EventRecord Record;

            public ReadEventCompleted(Guid correlationId, 
                                      string eventStreamId, 
                                      int eventNumber, 
                                      SingleReadResult result, 
                                      EventRecord record)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                Ensure.Nonnegative(eventNumber, "EventNumber");
                if (result == SingleReadResult.Success)
                    Ensure.NotNull(record, "record");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
                Result = result;
                Record = record;
            }
        }

        public abstract class ReadEventsMessage : ReadMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly string EventStreamId;
            public readonly int FromEventNumber;
            public readonly int MaxCount;
            public readonly bool ResolveLinks;

            protected ReadEventsMessage(Guid correlationId, 
                                        IEnvelope envelope, 
                                        string eventStreamId, 
                                        int fromEventNumber, 
                                        int maxCount,
                                        bool resolveLinks)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;
                ResolveLinks = resolveLinks;
            }

            public override string ToString()
            {
                return string.Format(GetType().Name + " CorrelationId: {0}, EventStreamId: {1}, FromEventNumber: {2}, MaxCount: {3}, ResolveLinks: {4}", CorrelationId, EventStreamId, FromEventNumber, MaxCount, ResolveLinks);
            }
        }

        public abstract class ReadEventsCompletedMessage : ReadMessage
        {
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly EventRecord[] Events;
            public readonly EventRecord[] LinkToEvents;
            public readonly RangeReadResult Result;
            public readonly int NextEventNumber;
            public readonly long? LastCommitPosition;

            protected ReadEventsCompletedMessage(Guid correlationId, 
                                                 string eventStreamId, 
                                                 EventRecord[] events,
                                                 EventRecord[] linkToEvents,
                                                 RangeReadResult result, 
                                                 int nextEventNumber,
                                                 long? lastCommitPosition)
            {
                Ensure.NotNull(events, "events");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                Events = events;
                LinkToEvents = linkToEvents;
                Result = result;
                NextEventNumber = nextEventNumber;
                LastCommitPosition = lastCommitPosition;
            }
        }

        public class ReadEventsFromTF : ReadMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long FromCommitPosition;
            public readonly long AfterPreparePosition;
            public readonly int MaxCount;
            public readonly bool ResolveLinks;

            public ReadEventsFromTF(Guid correlationId,
                                    IEnvelope envelope,
                                    long fromCommitPosition,
                                    long afterPreparePosition,
                                    int maxCount,
                                    bool resolveLinks)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                FromCommitPosition = fromCommitPosition;
                AfterPreparePosition = afterPreparePosition;
                MaxCount = maxCount;
                ResolveLinks = resolveLinks;
            }
        }

        public class ReadEventsFromTFCompleted : ReadMessage
        {
            public readonly Guid CorrelationId;
            public readonly ResolvedEventRecord[] Events;
            public readonly RangeReadResult Result;

            public ReadEventsFromTFCompleted(Guid correlationId, ResolvedEventRecord[] events, RangeReadResult result)
            {
                Ensure.NotNull(events, "events");

                CorrelationId = correlationId;
                Events = events;
                Result = result;
            }
        }

        public class ReadEventsBackwards : ReadEventsMessage
        {
            public ReadEventsBackwards(Guid correlationId, 
                                       IEnvelope envelope, 
                                       string eventStreamId, 
                                       int fromEventNumber, 
                                       int maxCount,
                                       bool resolveLinks)
                : base(correlationId, envelope, eventStreamId, fromEventNumber, maxCount, resolveLinks)
            {
            }
        }

        public class ReadEventsBackwardsCompleted : ReadEventsCompletedMessage
        {
            public ReadEventsBackwardsCompleted(Guid correlationId, 
                                                string eventStreamId, 
                                                EventRecord[] events,
                                                EventRecord[] linkToEvents,
                                                RangeReadResult result, 
                                                int nextEventNumber,
                                                long? lastCommitPosition)
                : base(correlationId, eventStreamId, events, linkToEvents, result, nextEventNumber, lastCommitPosition)
            {
            }
        }

        public class ReadEventsForward : ReadEventsMessage
        {
            public ReadEventsForward(Guid correlationId,
                                     IEnvelope envelope,
                                     string eventStreamId,
                                     int fromEventNumber,
                                     int maxCount,
                                     bool resolveLinks)
                : base(correlationId, envelope, eventStreamId, fromEventNumber, maxCount, resolveLinks)
            {
            }
        }

        public class ReadEventsForwardCompleted : ReadEventsCompletedMessage
        {
            public ReadEventsForwardCompleted(Guid correlationId,
                                              string eventStreamId,
                                              EventRecord[] events,
                                              EventRecord[] linkToEvents,
                                              RangeReadResult result,
                                              int nextEventNumber,
                                              long? lastCommitPosition)
                : base(correlationId, eventStreamId, events, linkToEvents, result, nextEventNumber, lastCommitPosition)
            {
            }
        }

        public class ListStreams : ReadMessage
        {
            public readonly IEnvelope Envelope;

            public ListStreams(IEnvelope envelope)
            {
                Envelope = envelope;
            }
        }

        public class ListStreamsCompleted : ReadMessage
        {
            public readonly string[] Streams;
            public readonly bool Success;

            public ListStreamsCompleted(bool success, string[] streams)
            {
                Success = success;
                Streams = streams;
            }
        }

        public class SubscribeToStream : ReadMessage
        {
            public readonly TcpConnectionManager Connection;
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;

            public SubscribeToStream(TcpConnectionManager connection, Guid correlationId, string eventStreamId)
            {
                Connection = connection;
                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
            }
        }

        public class UnsubscribeFromStream : ReadMessage
        {
            public readonly TcpConnectionManager Connection;
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;

            public UnsubscribeFromStream(TcpConnectionManager connection, Guid correlationId, string eventStreamId)
            {
                Connection = connection;
                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
            }
        }

        public class SubscribeToAllStreams : ReadMessage
        {
            public readonly TcpConnectionManager Connection;
            public readonly Guid CorrelationId;

            public SubscribeToAllStreams(TcpConnectionManager connection, Guid correlationId)
            {
                Connection = connection;
                CorrelationId = correlationId;
            }
        }

        public class UnsubscribeFromAllStreams : ReadMessage
        {
            public readonly TcpConnectionManager Connection;
            public readonly Guid CorrelationId;

            public UnsubscribeFromAllStreams(TcpConnectionManager connection, Guid correlationId)
            {
                Connection = connection;
                CorrelationId = correlationId;
            }
        }

        public class StreamEventAppeared : Message
        {
            public readonly Guid CorrelationId;
            public readonly int EventNumber;
            public readonly PrepareLogRecord Event;

            public StreamEventAppeared(Guid correlationId, int eventNumber, PrepareLogRecord @event)
            {
                CorrelationId = correlationId;
                EventNumber = eventNumber;
                Event = @event;
            }
        }

        public class SubscriptionDropped: Message
        {
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;

            public SubscriptionDropped(Guid correlationId, string eventStreamId)
            {
                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
            }
        }

        public class SubscriptionToAllDropped : Message
        {
            public readonly Guid CorrelationId;

            public SubscriptionToAllDropped(Guid correlationId)
            {
                CorrelationId = correlationId;
            }
        }
    }
}