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
        PrepareTimeout = 1,
        CommitTimeout = 2,
        ForwardTimeout = 3,
        WrongExpectedVersion = 4,
        StreamDeleted = 5,
        InvalidTransaction = 6
    }

    public static class ClientMessage
    {
        public class RequestShutdown: Message
        {
            public readonly bool ExitProcessOnShutdown;

            public RequestShutdown(bool exitProcessOnShutdown)
            {
                ExitProcessOnShutdown = exitProcessOnShutdown;
            }
        }

        public abstract class WriteRequestMessage : Message
        {
        }

        public abstract class WriteResponseMessage : Message
        {
        }

        public abstract class ReadRequestMessage: Message
        {
        }

        public abstract class ReadResponseMessage : Message
        {
            internal static readonly EventLinkPair[] EmptyRecords = new EventLinkPair[0];
        }

        public class ForwardMessage: Message
        {
            public readonly Message Message;

            public ForwardMessage(Message message)
            {
                Message = message;
            }
        }

        public class DeniedToRoute : Message
        {
            public readonly Guid CorrelationId;

            public readonly IPEndPoint ExternalTcpEndPoint;
            public readonly IPEndPoint ExternalHttpEndPoint;

            public DeniedToRoute(Guid correlationId,
                                 IPEndPoint externalTcpEndPoint,
                                 IPEndPoint externalHttpEndPoint)
            {
                CorrelationId = correlationId;
                ExternalTcpEndPoint = externalTcpEndPoint;
                ExternalHttpEndPoint = externalHttpEndPoint;
            }
        }

        public class CreateStream: WriteRequestMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly bool AllowForwarding;

            public readonly string EventStreamId;
            public readonly Guid CreateStreamId;
            public readonly bool IsJson;
            public readonly byte[] Metadata;

            public CreateStream(Guid correlationId, IEnvelope envelope, bool allowForwarding, string eventStreamId, Guid createStreamId, bool isJson, byte[] metadata)
            {
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");
                Ensure.NotEmptyGuid(createStreamId, "createStreamId");
                
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                AllowForwarding = allowForwarding;
                EventStreamId = eventStreamId;
                CreateStreamId = createStreamId;
                IsJson = isJson;
                Metadata = metadata;
            }
        }

        public class CreateStreamCompleted : WriteResponseMessage
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

        public class WriteEvents : WriteRequestMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly bool AllowForwarding;

            public readonly string EventStreamId;
            public readonly int ExpectedVersion;
            public readonly Event[] Events;

            public WriteEvents(Guid correlationId, IEnvelope envelope, bool allowForwarding, string eventStreamId, int expectedVersion, Event[] events)
            {
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");
                Ensure.NotNull(events, "events");
                Ensure.Positive(events.Length, "events.Length");

                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                AllowForwarding = allowForwarding;

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                Events = events;
            }

            public WriteEvents(Guid correlationId, IEnvelope envelope, bool allowForwarding, string eventStreamId, int expectedVersion, Event @event)
                : this(correlationId, envelope, allowForwarding, eventStreamId, expectedVersion, new[]{@event})
            {
            }

            public override string ToString()
            {
                return string.Format("WRITE: CorrelationId: {0}, EventStreamId: {1}, ExpectedVersion: {2}, Events: {3}", CorrelationId, EventStreamId, ExpectedVersion, Events.Length);
            }
        }

        public class WriteEventsCompleted : WriteResponseMessage
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

        public class TransactionStart : WriteRequestMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly bool AllowForwarding;

            public readonly string EventStreamId;
            public readonly int ExpectedVersion;

            public TransactionStart(Guid correlationId, IEnvelope envelope, bool allowForwarding, string eventStreamId, int expectedVersion)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                AllowForwarding = allowForwarding;
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class TransactionStartCompleted : WriteResponseMessage
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

        public class TransactionWrite : WriteRequestMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly bool AllowForwarding;

            public readonly long TransactionId;
            public readonly string EventStreamId;
            public readonly Event[] Events;

            public TransactionWrite(Guid correlationId, IEnvelope envelope, bool allowForwarding, long transactionId, string eventStreamId, Event[] events)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                AllowForwarding = allowForwarding;
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                Events = events;
            }
        }

        public class TransactionWriteCompleted : WriteResponseMessage
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

        public class TransactionCommit : WriteRequestMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly bool AllowForwarding;

            public readonly long TransactionId;
            public readonly string EventStreamId;

            public TransactionCommit(Guid correlationId, IEnvelope envelope, bool allowForwarding, long transactionId, string eventStreamId)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                AllowForwarding = allowForwarding;
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
            }
        }

        public class TransactionCommitCompleted : WriteResponseMessage
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

        public class DeleteStream : WriteRequestMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly bool AllowForwarding;

            public readonly string EventStreamId;
            public readonly int ExpectedVersion;

            public DeleteStream(Guid correlationId,
                                IEnvelope envelope,
                                bool allowForwarding,
                                string eventStreamId,
                                int expectedVersion)
            {
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");

                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                AllowForwarding = allowForwarding;
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class DeleteStreamCompleted : WriteResponseMessage
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

        public class ReadEvent : ReadRequestMessage
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

        public class ReadEventCompleted : ReadResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly int EventNumber;
            public readonly SingleReadResult Result;
            public readonly EventLinkPair Record;

            public ReadEventCompleted(Guid correlationId, 
                                      string eventStreamId, 
                                      int eventNumber, 
                                      SingleReadResult result, 
                                      EventLinkPair record)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                Ensure.Nonnegative(eventNumber, "EventNumber");
                if (result == SingleReadResult.Success)
                    Ensure.NotNull(record.Event, "record.Event");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
                Result = result;
                Record = record;
            }
        }

        public class ReadStreamEventsForward : ReadRequestMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly string EventStreamId;
            public readonly int FromEventNumber;
            public readonly int MaxCount;
            public readonly bool ResolveLinks;

            public readonly int? ValidationStreamVersion;

            public ReadStreamEventsForward(Guid correlationId,
                                           IEnvelope envelope,
                                           string eventStreamId,
                                           int fromEventNumber,
                                           int maxCount,
                                           bool resolveLinks)
                : this(correlationId, envelope, eventStreamId, fromEventNumber, maxCount, resolveLinks, null)
            {
                
            }

            public ReadStreamEventsForward(Guid correlationId,
                                           IEnvelope envelope,
                                           string eventStreamId,
                                           int fromEventNumber,
                                           int maxCount,
                                           bool resolveLinks,
                                           int? validationStreamVersion)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;
                ResolveLinks = resolveLinks;
                ValidationStreamVersion = validationStreamVersion;
            }

            public override string ToString()
            {
                return string.Format(GetType().Name + " CorrelationId: {0}, EventStreamId: {1}, FromEventNumber: {2}, MaxCount: {3}, ResolveLinks: {4}, ValidationStreamVersion: {5}",
                                     CorrelationId,
                                     EventStreamId,
                                     FromEventNumber,
                                     MaxCount,
                                     ResolveLinks,
                                     ValidationStreamVersion);
            }
        }

        public class ReadStreamEventsForwardCompleted : ReadResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly int FromEventNumber;
            public readonly int MaxCount;
            
            public readonly StreamResult Result;
            public readonly EventLinkPair[] Events;
            public readonly string Error;
            public readonly int NextEventNumber;
            public readonly int LastEventNumber;
            public readonly bool IsEndOfStream;
            public readonly long? LastCommitPosition;

            public ReadStreamEventsForwardCompleted(Guid correlationId,
                                                    string eventStreamId,
                                                    int fromEventNumber,
                                                    int maxCount,
                                                    StreamResult result,
                                                    EventLinkPair[] events,
                                                    string error,
                                                    int nextEventNumber,
                                                    int lastEventNumber,
                                                    bool isEndOfStream,
                                                    long? lastCommitPosition)
            {
                Ensure.NotNull(events, "events");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;

                Result = result;
                Events = events;
                Error = error;
                NextEventNumber = nextEventNumber;
                LastEventNumber = lastEventNumber;
                IsEndOfStream = isEndOfStream;
                LastCommitPosition = lastCommitPosition;
            }

            public static ReadStreamEventsForwardCompleted NotModified(Guid correlationId, string eventStreamId, int fromEventNumber, int maxCount)
            {
                return new ReadStreamEventsForwardCompleted(correlationId, eventStreamId, fromEventNumber, maxCount, 
                                                            StreamResult.NotModified, EmptyRecords, string.Empty, -1, -1, false, null);
            }

            public static ReadStreamEventsForwardCompleted Faulted(Guid correlationId, string eventStreamId, int fromEventNumber, int maxCount, string error)
            {
                Ensure.NotNullOrEmpty(error, "error");
                return new ReadStreamEventsForwardCompleted(correlationId, eventStreamId, fromEventNumber, maxCount,
                                                            StreamResult.Error, EmptyRecords, error, -1, -1, false, null);
            }
        }

        public class ReadStreamEventsBackward : ReadRequestMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly string EventStreamId;
            public readonly int FromEventNumber;
            public readonly int MaxCount;
            public readonly bool ResolveLinks;

            public readonly int? ValidationStreamVersion;

            public ReadStreamEventsBackward(Guid correlationId,
                                            IEnvelope envelope,
                                            string eventStreamId,
                                            int fromEventNumber,
                                            int maxCount,
                                            bool resolveLinks,
                                            int? validationStreamVersion)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;
                ResolveLinks = resolveLinks;
                ValidationStreamVersion = validationStreamVersion;
            }

            public override string ToString()
            {
                return string.Format(GetType().Name + " CorrelationId: {0}, EventStreamId: {1}, FromEventNumber: {2}, MaxCount: {3}, ResolveLinks: {4}, ValidationStreamVersion: {5}", CorrelationId, EventStreamId, FromEventNumber, MaxCount, ResolveLinks, ValidationStreamVersion);
            }
        }

        public class ReadStreamEventsBackwardCompleted : ReadResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly int FromEventNumber;
            public readonly int MaxCount;

            public readonly StreamResult Result;
            public readonly EventLinkPair[] Events;
            public readonly string Error;
            public readonly int NextEventNumber;
            public readonly int LastEventNumber;
            public readonly bool IsEndOfStream;
            public readonly long? LastCommitPosition;

            public ReadStreamEventsBackwardCompleted(Guid correlationId,
                                                     string eventStreamId,
                                                     int fromEventNumber,
                                                     int maxCount,
                                                     StreamResult result,
                                                     EventLinkPair[] events,
                                                     string error,
                                                     int nextEventNumber,
                                                     int lastEventNumber,
                                                     bool isEndOfStream,
                                                     long? lastCommitPosition)
            {
                Ensure.NotNull(events, "events");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;

                Result = result;
                Events = events;
                Error = error;
                NextEventNumber = nextEventNumber;
                LastEventNumber = lastEventNumber;
                IsEndOfStream = isEndOfStream;
                LastCommitPosition = lastCommitPosition;
            }

            public static ReadStreamEventsBackwardCompleted NotModified(Guid correlationId, string eventStreamId, int fromEventNumber, int maxCount)
            {
                return new ReadStreamEventsBackwardCompleted(correlationId, eventStreamId, fromEventNumber, maxCount,
                                                             StreamResult.NotModified, EmptyRecords, string.Empty, -1, -1, false, null);
            }

            public static ReadStreamEventsBackwardCompleted Faulted(Guid correlationId, string eventStreamId, int fromEventNumber, int maxCount, string error)
            {
                Ensure.NotNullOrEmpty(error, "error");
                return new ReadStreamEventsBackwardCompleted(correlationId, eventStreamId, fromEventNumber, maxCount,
                                                             StreamResult.Error, EmptyRecords, error, -1, -1, false, null);
            }
        }

        public class ReadAllEventsForward : ReadRequestMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long CommitPosition;
            public readonly long PreparePosition;
            public readonly int MaxCount;
            public readonly bool ResolveLinks;

            public readonly long? ValidationTfEofPosition;

            public ReadAllEventsForward(Guid correlationId,
                                        IEnvelope envelope,
                                        long commitPosition,
                                        long preparePosition,
                                        int maxCount,
                                        bool resolveLinks,
                                        long? validationTfEofPosition)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                MaxCount = maxCount;
                ResolveLinks = resolveLinks;

                ValidationTfEofPosition = validationTfEofPosition;
            }
        }

        public class ReadAllEventsForwardCompleted : ReadResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly ReadAllResult Result;
            public readonly bool NotModified;

            public ReadAllEventsForwardCompleted(Guid correlationId, ReadAllResult result, bool notModified)
            {
                CorrelationId = correlationId;
                NotModified = notModified;
                Result = result;
            }
        }

        public class ReadAllEventsBackward : ReadRequestMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long CommitPosition;
            public readonly long PreparePosition;
            public readonly int MaxCount;
            public readonly bool ResolveLinks;

            public readonly long? ValidationTfEofPosition;

            public ReadAllEventsBackward(Guid correlationId,
                                         IEnvelope envelope,
                                         long commitPosition,
                                         long preparePosition,
                                         int maxCount,
                                         bool resolveLinks,
                                         long? validationTfEofPosition)
            {
                CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
                Envelope = envelope;
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                MaxCount = maxCount;
                ResolveLinks = resolveLinks;

                ValidationTfEofPosition = validationTfEofPosition;
            }
        }

        public class ReadAllEventsBackwardCompleted : ReadResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly ReadAllResult Result;
            public readonly bool NotModified;

            public ReadAllEventsBackwardCompleted(Guid correlationId, ReadAllResult result, bool notModified)
            {
                CorrelationId = correlationId;
                NotModified = notModified;
                Result = result;
            }
        }

        public class ListStreams : ReadRequestMessage
        {
            public readonly IEnvelope Envelope;

            public ListStreams(IEnvelope envelope)
            {
                Envelope = envelope;
            }
        }

        public class ListStreamsCompleted : ReadResponseMessage
        {
            public readonly string[] Streams;
            public readonly bool Success;

            public ListStreamsCompleted(bool success, string[] streams)
            {
                Success = success;
                Streams = streams;
            }
        }

        public class SubscribeToStream : ReadRequestMessage
        {
            public readonly TcpConnectionManager Connection;
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly bool ResolveLinkTos;

            public SubscribeToStream(TcpConnectionManager connection, Guid correlationId, string eventStreamId, bool resolveLinkTos)
            {
                Connection = connection;
                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        public class UnsubscribeFromStream : ReadRequestMessage
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

        public class SubscribeToAllStreams : ReadRequestMessage
        {
            public readonly TcpConnectionManager Connection;
            public readonly Guid CorrelationId;
            public readonly bool ResolveLinkTos;

            public SubscribeToAllStreams(TcpConnectionManager connection, Guid correlationId, bool resolveLinkTos)
            {
                Connection = connection;
                CorrelationId = correlationId;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        public class UnsubscribeFromAllStreams : ReadRequestMessage
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
            public readonly long CommitPosition;

            public StreamEventAppeared(Guid correlationId, int eventNumber, PrepareLogRecord @event, long commitPosition)
            {
                CorrelationId = correlationId;
                EventNumber = eventNumber;
                Event = @event;
                CommitPosition = commitPosition;
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