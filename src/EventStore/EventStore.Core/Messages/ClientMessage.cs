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
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;

namespace EventStore.Core.Messages
{
    public enum OperationResult
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
            public readonly bool ExitProcess;

            public RequestShutdown(bool exitProcess)
            {
                ExitProcess = exitProcess;
            }
        }

        public abstract class WriteRequestMessage : Message
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly bool AllowForwarding;

            protected WriteRequestMessage(Guid correlationId, IEnvelope envelope, bool allowForwarding)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");

                CorrelationId = correlationId;
                Envelope = envelope;
                AllowForwarding = allowForwarding;
            }
        }

        public abstract class WriteResponseMessage : Message
        {
        }

        public abstract class ReadRequestMessage: Message
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;

            protected ReadRequestMessage(Guid correlationId, IEnvelope envelope)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");

                CorrelationId = correlationId;
                Envelope = envelope;
            }
        }

        public abstract class ReadResponseMessage : Message
        {
            internal static readonly ResolvedEvent[] EmptyRecords = new ResolvedEvent[0];
        }

        public class ForwardMessage: Message
        {
            public readonly Message Message;

            public ForwardMessage(Message message)
            {
                Message = message;
            }
        }

        public class NotHandled : Message
        {
            public readonly Guid CorrelationId;
            public readonly TcpClientMessageDto.NotHandled.NotHandledReason Reason;
            public readonly object AdditionalInfo;

            public NotHandled(Guid correlationId,
                              TcpClientMessageDto.NotHandled.NotHandledReason reason,
                              object additionalInfo)
            {
                CorrelationId = correlationId;
                Reason = reason;
                AdditionalInfo = additionalInfo;
            }

            public class MasterInfo
            {
                public readonly IPEndPoint ExternalTcpEndPoint;
                public readonly IPEndPoint ExternalHttpEndPoint;

                public MasterInfo(IPEndPoint externalTcpEndPoint, IPEndPoint externalHttpEndPoint)
                {
                    Ensure.NotNull(externalTcpEndPoint, "externalTcpEndPoint");
                    Ensure.NotNull(externalHttpEndPoint, "externalHttpEndPoint");
                    ExternalTcpEndPoint = externalTcpEndPoint;
                    ExternalHttpEndPoint = externalHttpEndPoint;
                }
            }
        }

        public class CreateStream: WriteRequestMessage
        {
            public readonly string EventStreamId;
            public readonly Guid RequestId;
            public readonly bool IsJson;
            public readonly byte[] Metadata;

            public CreateStream(Guid correlationId, IEnvelope envelope, bool allowForwarding, 
                                string eventStreamId, Guid requestId, bool isJson, byte[] metadata)
                : base(correlationId, envelope, allowForwarding)
            {
                Ensure.NotNull(eventStreamId, "eventStreamId");
                Ensure.NotEmptyGuid(requestId, "requestId");
                
                EventStreamId = eventStreamId;
                RequestId = requestId;
                IsJson = isJson;
                Metadata = metadata;
            }
        }

        public class CreateStreamCompleted : WriteResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly OperationResult Result;
            public readonly string Message;

            public CreateStreamCompleted(Guid correlationId, OperationResult result, string message)
            {
                CorrelationId = correlationId;
                Result = result;
                Message = message;
            }
        }

        public class WriteEvents : WriteRequestMessage
        {
            public readonly string EventStreamId;
            public readonly int ExpectedVersion;
            public readonly Event[] Events;

            public WriteEvents(Guid correlationId, IEnvelope envelope, bool allowForwarding, 
                               string eventStreamId, int expectedVersion, Event[] events)
                : base(correlationId, envelope, allowForwarding)
            {
                Ensure.NotNull(eventStreamId, "eventStreamId");
                Ensure.NotNull(events, "events");
                Ensure.Positive(events.Length, "events.Length");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                Events = events;
            }

            public WriteEvents(Guid correlationId, IEnvelope envelope, bool allowForwarding, 
                               string eventStreamId, int expectedVersion, Event @event)
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
            public readonly OperationResult Result;
            public readonly string Message;
            public readonly int FirstEventNumber;

            public WriteEventsCompleted(Guid correlationId, int firstEventNumber)
            {
                Ensure.Nonnegative(firstEventNumber, "firstEventNumber");

                CorrelationId = correlationId;
                Result = OperationResult.Success;
                Message = null;
                FirstEventNumber = firstEventNumber;
            }

            public WriteEventsCompleted(Guid correlationId, OperationResult result, string message)
            {
                if (result == OperationResult.Success)
                    throw new ArgumentException("Invalid constructor used for successful write.", "result");

                CorrelationId = correlationId;
                Result = result;
                Message = message;
                FirstEventNumber = EventNumber.Invalid;
            }
        }

        public class TransactionStart : WriteRequestMessage
        {
            public readonly string EventStreamId;
            public readonly int ExpectedVersion;

            public TransactionStart(Guid correlationId, IEnvelope envelope, bool allowForwarding, string eventStreamId, int expectedVersion)
                : base(correlationId, envelope, allowForwarding)
            {
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class TransactionStartCompleted : WriteResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly long TransactionId;
            public readonly OperationResult Result;
            public readonly string Message;

            public TransactionStartCompleted(Guid correlationId, long transactionId, OperationResult result, string message)
            {
                CorrelationId = correlationId;
                TransactionId = transactionId;
                Result = result;
                Message = message;
            }
        }

        public class TransactionWrite : WriteRequestMessage
        {
            public readonly long TransactionId;
            public readonly Event[] Events;

            public TransactionWrite(Guid correlationId, IEnvelope envelope, bool allowForwarding, long transactionId, Event[] events)
                : base(correlationId, envelope, allowForwarding)
            {
                TransactionId = transactionId;
                Events = events;
            }
        }

        public class TransactionWriteCompleted : WriteResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly long TransactionId;
            public readonly OperationResult Result;
            public readonly string Message;

            public TransactionWriteCompleted(Guid correlationId, long transactionId, OperationResult result, string message)
            {
                CorrelationId = correlationId;
                TransactionId = transactionId;
                Result = result;
                Message = message;
            }
        }

        public class TransactionCommit : WriteRequestMessage
        {
            public readonly long TransactionId;

            public TransactionCommit(Guid correlationId, IEnvelope envelope, bool allowForwarding, long transactionId)
                : base(correlationId, envelope, allowForwarding)
            {
                TransactionId = transactionId;
            }
        }

        public class TransactionCommitCompleted : WriteResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly long TransactionId;
            public readonly OperationResult Result;
            public readonly string Message;

            public TransactionCommitCompleted(Guid correlationId, long transactionId, OperationResult result, string message)
            {
                CorrelationId = correlationId;
                TransactionId = transactionId;
                Result = result;
                Message = message;
            }
        }

        public class DeleteStream : WriteRequestMessage
        {
            public readonly string EventStreamId;
            public readonly int ExpectedVersion;

            public DeleteStream(Guid correlationId, IEnvelope envelope, bool allowForwarding, string eventStreamId, int expectedVersion)
                : base(correlationId, envelope, allowForwarding)
            {
                Ensure.NotNull(eventStreamId, "eventStreamId");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class DeleteStreamCompleted : WriteResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly OperationResult Result;
            public readonly string Message;

            public DeleteStreamCompleted(Guid correlationId, OperationResult result, string message)
            {
                CorrelationId = correlationId;
                Result = result;
                Message = message;
            }
        }

        public class ReadEvent : ReadRequestMessage
        {
            public readonly string EventStreamId;
            public readonly int EventNumber;
            public readonly bool ResolveLinkTos;

            public ReadEvent(Guid correlationId, IEnvelope envelope, string eventStreamId, int eventNumber, bool resolveLinkTos)
                : base(correlationId, envelope)
            {
                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        public class ReadEventCompleted : ReadResponseMessage
        {
            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly ReadEventResult Result;
            public readonly ResolvedEvent Record;

            public ReadEventCompleted(Guid correlationId, string eventStreamId, ReadEventResult result, ResolvedEvent record)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (result == ReadEventResult.Success)
                    Ensure.NotNull(record.Event, "record.Event");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                Result = result;
                Record = record;
            }
        }

        public class ReadStreamEventsForward : ReadRequestMessage
        {
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
                : base(correlationId, envelope)
            {
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
            
            public readonly ReadStreamResult Result;
            public readonly ResolvedEvent[] Events;
            public readonly string Message;
            public readonly int NextEventNumber;
            public readonly int LastEventNumber;
            public readonly bool IsEndOfStream;
            public readonly long LastCommitPosition;

            public ReadStreamEventsForwardCompleted(Guid correlationId,
                                                    string eventStreamId,
                                                    int fromEventNumber,
                                                    int maxCount,
                                                    ReadStreamResult result,
                                                    ResolvedEvent[] events,
                                                    string message,
                                                    int nextEventNumber,
                                                    int lastEventNumber,
                                                    bool isEndOfStream,
                                                    long lastCommitPosition)
            {
                Ensure.NotNull(events, "events");

                if (result != ReadStreamResult.Success)
                {
                    Ensure.Equal(nextEventNumber, -1, "nextEventNumber");
                    Ensure.Equal(lastEventNumber, -1, "lastEventNumber");
                    Ensure.Equal(isEndOfStream, true, "isEndOfStream");
                }

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;

                Result = result;
                Events = events;
                Message = message;
                NextEventNumber = nextEventNumber;
                LastEventNumber = lastEventNumber;
                IsEndOfStream = isEndOfStream;
                LastCommitPosition = lastCommitPosition;
            }

            public static ReadStreamEventsForwardCompleted NotModified(Guid correlationId, string eventStreamId, 
                                                                       int fromEventNumber, int maxCount, long lastCommitPosition)
            {
                return new ReadStreamEventsForwardCompleted(correlationId, eventStreamId, fromEventNumber, maxCount, 
                                                            ReadStreamResult.NotModified, EmptyRecords, string.Empty, -1, -1, true, lastCommitPosition);
            }

            public static ReadStreamEventsForwardCompleted Faulted(Guid correlationId, string eventStreamId, int fromEventNumber, int maxCount, 
                                                                   string message, long lastCommitPosition)
            {
                Ensure.NotNullOrEmpty(message, "message");
                return new ReadStreamEventsForwardCompleted(correlationId, eventStreamId, fromEventNumber, maxCount,
                                                            ReadStreamResult.Error, EmptyRecords, message, -1, -1, true, lastCommitPosition);
            }
        }

        public class ReadStreamEventsBackward : ReadRequestMessage
        {
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
                : base(correlationId, envelope)
            {
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

            public readonly ReadStreamResult Result;
            public readonly ResolvedEvent[] Events;
            public readonly string Message;
            public readonly int NextEventNumber;
            public readonly int LastEventNumber;
            public readonly bool IsEndOfStream;
            public readonly long LastCommitPosition;

            public ReadStreamEventsBackwardCompleted(Guid correlationId,
                                                     string eventStreamId,
                                                     int fromEventNumber,
                                                     int maxCount,
                                                     ReadStreamResult result,
                                                     ResolvedEvent[] events,
                                                     string message,
                                                     int nextEventNumber,
                                                     int lastEventNumber,
                                                     bool isEndOfStream,
                                                     long lastCommitPosition)
            {
                Ensure.NotNull(events, "events");

                if (result != ReadStreamResult.Success)
                {
                    Ensure.Equal(nextEventNumber, -1, "nextEventNumber");
                    Ensure.Equal(lastEventNumber, -1, "lastEventNumber");
                    Ensure.Equal(isEndOfStream, true, "isEndOfStream");
                }

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;

                Result = result;
                Events = events;
                Message = message;
                NextEventNumber = nextEventNumber;
                LastEventNumber = lastEventNumber;
                IsEndOfStream = isEndOfStream;
                LastCommitPosition = lastCommitPosition;
            }

            public static ReadStreamEventsBackwardCompleted NotModified(Guid correlationId, string eventStreamId, 
                                                                        int fromEventNumber, int maxCount, long lastCommitPosition)
            {
                return new ReadStreamEventsBackwardCompleted(correlationId, eventStreamId, fromEventNumber, maxCount,
                                                             ReadStreamResult.NotModified, EmptyRecords, string.Empty, -1, -1, true, lastCommitPosition);
            }

            public static ReadStreamEventsBackwardCompleted Faulted(Guid correlationId, string eventStreamId, int fromEventNumber, 
                                                                    int maxCount, string message, long lastCommitPosition)
            {
                Ensure.NotNullOrEmpty(message, "message");
                return new ReadStreamEventsBackwardCompleted(correlationId, eventStreamId, fromEventNumber, maxCount,
                                                             ReadStreamResult.Error, EmptyRecords, message, -1, -1, true, lastCommitPosition);
            }
        }

        public class ReadAllEventsForward : ReadRequestMessage
        {
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
                : base(correlationId, envelope)
            {
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
                : base(correlationId, envelope)
            {
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

        public class SubscribeToStream : ReadRequestMessage
        {
            public readonly Guid ConnectionId;
            public readonly string EventStreamId; // should be empty to subscribe to all
            public readonly bool ResolveLinkTos;

            public SubscribeToStream(Guid correlationId, IEnvelope envelope, Guid connectionId, string eventStreamId, bool resolveLinkTos)
                : base(correlationId, envelope)
            {
                Ensure.NotEmptyGuid(connectionId, "connectionId");
                ConnectionId = connectionId;
                EventStreamId = eventStreamId;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        public class UnsubscribeFromStream : ReadRequestMessage
        {
            public UnsubscribeFromStream(Guid correlationId, IEnvelope envelope)
                : base(correlationId, envelope)
            {
            }
        }

        public class SubscriptionConfirmation : Message
        {
            public readonly Guid CorrelationId;
            public readonly long LastCommitPosition;
            public readonly int? LastEventNumber;

            public SubscriptionConfirmation(Guid correlationId, long lastCommitPosition, int? lastEventNumber)
            {
                CorrelationId = correlationId;
                LastCommitPosition = lastCommitPosition;
                LastEventNumber = lastEventNumber;
            }
        }

        public class StreamEventAppeared : Message
        {
            public readonly Guid CorrelationId;
            public readonly ResolvedEvent Event;

            public StreamEventAppeared(Guid correlationId, ResolvedEvent @event)
            {
                CorrelationId = correlationId;
                Event = @event;
            }
        }

        public class SubscriptionDropped: Message
        {
            public readonly Guid CorrelationId;

            public SubscriptionDropped(Guid correlationId)
            {
                CorrelationId = correlationId;
            }
        }
    }
}