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
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
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
        InvalidTransaction = 6,
        AccessDenied = 7
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

            public readonly IPrincipal User;
            public readonly string Login;
            public readonly string Password;

            protected WriteRequestMessage(Guid correlationId, IEnvelope envelope, bool allowForwarding,
                                          IPrincipal user, string login, string password)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");

                CorrelationId = correlationId;
                Envelope = envelope;
                AllowForwarding = allowForwarding;

                User = user;
                Login = login;
                Password = password;
            }
        }

        public abstract class ReadRequestMessage: Message
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;

            public readonly IPrincipal User;

            protected ReadRequestMessage(Guid correlationId, IEnvelope envelope, IPrincipal user)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");

                CorrelationId = correlationId;
                Envelope = envelope;

                User = user;
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
                Ensure.NotNull(message, "message");

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
        }

        public class WriteEvents : WriteRequestMessage
        {
            public readonly string EventStreamId;
            public readonly int ExpectedVersion;
            public readonly Event[] Events;

            public WriteEvents(Guid correlationId, IEnvelope envelope, bool allowForwarding, 
                               string eventStreamId, int expectedVersion, Event[] events,
                               IPrincipal user, string login = null, string password = null)
                : base(correlationId, envelope, allowForwarding, user, login, password)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (expectedVersion < Data.ExpectedVersion.Any) throw new ArgumentOutOfRangeException("expectedVersion");
                Ensure.NotNull(events, "events");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                Events = events;
            }

            public WriteEvents(Guid correlationId, IEnvelope envelope, bool allowForwarding,
                               string eventStreamId, int expectedVersion, Event @event, IPrincipal user)
                : this(correlationId, envelope, allowForwarding, eventStreamId, expectedVersion, new[] { @event }, user)
            {
            }

            public override string ToString()
            {
                return string.Format("WRITE: CorrelationId: {0}, EventStreamId: {1}, ExpectedVersion: {2}, Events: {3}", CorrelationId, EventStreamId, ExpectedVersion, Events.Length);
            }
        }

        public class WriteEventsCompleted : Message
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

            public TransactionStart(Guid correlationId, IEnvelope envelope, bool allowForwarding,
                                    string eventStreamId, int expectedVersion,
                                    IPrincipal user, string login = null, string password = null)
                : base(correlationId, envelope, allowForwarding, user, login, password)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (expectedVersion < Data.ExpectedVersion.Any) throw new ArgumentOutOfRangeException("expectedVersion");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class TransactionStartCompleted : Message
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

            public TransactionWrite(Guid correlationId, IEnvelope envelope, bool allowForwarding, long transactionId,
                                    Event[] events, IPrincipal user, string login = null, string password = null)
                : base(correlationId, envelope, allowForwarding, user, login, password)
            {
                Ensure.Nonnegative(transactionId, "transactionId");
                Ensure.NotNull(events, "events");

                TransactionId = transactionId;
                Events = events;
            }
        }

        public class TransactionWriteCompleted : Message
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

            public TransactionCommit(Guid correlationId, IEnvelope envelope, bool allowForwarding, long transactionId,
                                     IPrincipal user, string login = null, string password = null)
                : base(correlationId, envelope, allowForwarding, user, login, password)
            {
                Ensure.Nonnegative(transactionId, "transactionId");
                TransactionId = transactionId;
            }
        }

        public class TransactionCommitCompleted : Message
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

            public DeleteStream(Guid correlationId, IEnvelope envelope, bool allowForwarding,
                                string eventStreamId, int expectedVersion,
                                IPrincipal user, string login = null, string password = null)
                : base(correlationId, envelope, allowForwarding, user, login, password)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (expectedVersion < Data.ExpectedVersion.Any) throw new ArgumentOutOfRangeException("expectedVersion");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class DeleteStreamCompleted : Message
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

            public ReadEvent(Guid correlationId, IEnvelope envelope, string eventStreamId, int eventNumber,
                             bool resolveLinkTos, IPrincipal user)
                : base(correlationId, envelope, user)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (eventNumber < -1) throw new ArgumentOutOfRangeException("eventNumber");

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
            public readonly string Error;

            public ReadEventCompleted(Guid correlationId, string eventStreamId, ReadEventResult result, ResolvedEvent record, string error)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (result == ReadEventResult.Success)
                    Ensure.NotNull(record.Event, "record.Event");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                Result = result;
                Record = record;
                Error = error;
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
                                           bool resolveLinks,
                                           int? validationStreamVersion,
                                           IPrincipal user)
                : base(correlationId, envelope, user)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (fromEventNumber < -1) throw new ArgumentOutOfRangeException("fromEventNumber");

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
            public readonly string Error;
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
                                                    string error,
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
                Error = error;
                NextEventNumber = nextEventNumber;
                LastEventNumber = lastEventNumber;
                IsEndOfStream = isEndOfStream;
                LastCommitPosition = lastCommitPosition;
            }

            public static ReadStreamEventsForwardCompleted NoRecords(ReadStreamResult result, Guid correlationId,  string eventStreamId,
                                                                     int fromEventNumber, int maxCount, long lastCommitPosition, string message = null)
            {
                return new ReadStreamEventsForwardCompleted(correlationId, eventStreamId, fromEventNumber, maxCount, 
                                                            result, EmptyRecords, message ?? string.Empty, 
                                                            -1, -1, true, lastCommitPosition);
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
                                            int? validationStreamVersion,
                                            IPrincipal user)
                : base(correlationId, envelope, user)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (fromEventNumber < -1) throw new ArgumentOutOfRangeException("fromEventNumber");

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
            public readonly string Error;
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
                                                     string error,
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
                Error = error;
                NextEventNumber = nextEventNumber;
                LastEventNumber = lastEventNumber;
                IsEndOfStream = isEndOfStream;
                LastCommitPosition = lastCommitPosition;
            }

            public static ReadStreamEventsBackwardCompleted NoRecords(ReadStreamResult result, Guid correlationId, string eventStreamId,
                                                                      int fromEventNumber, int maxCount, long lastCommitPosition, string message = null)
            {
                return new ReadStreamEventsBackwardCompleted(correlationId, eventStreamId, fromEventNumber, maxCount,
                                                             result, EmptyRecords, message ?? string.Empty,
                                                             -1, -1, true, lastCommitPosition);
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
                                        long? validationTfEofPosition, 
                                        IPrincipal user)
                : base(correlationId, envelope, user)
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
            public readonly string Error;

            public readonly ResolvedEvent[] Events;
            public readonly int MaxCount;
            public readonly TFPos CurrentPos;
            public readonly TFPos NextPos;
            public readonly TFPos PrevPos;
            public readonly long TfEofPosition;

            public ReadAllEventsForwardCompleted(Guid correlationId, ReadAllResult result, string error, ResolvedEvent[] events, 
                                                 int maxCount, TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfEofPosition)
            {
                Ensure.NotNull(events, "events");

                CorrelationId = correlationId;
                Result = result;
                Error = error;
                Events = events;
                MaxCount = maxCount;
                CurrentPos = currentPos;
                NextPos = nextPos;
                PrevPos = prevPos;
                TfEofPosition = tfEofPosition;
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
                                         long? validationTfEofPosition, 
                                         IPrincipal user)
                : base(correlationId, envelope, user)
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
            public readonly string Error;

            public readonly ResolvedEvent[] Events;
            public readonly int MaxCount;
            public readonly TFPos CurrentPos;
            public readonly TFPos NextPos;
            public readonly TFPos PrevPos;
            public readonly long TfEofPosition;

            public ReadAllEventsBackwardCompleted(Guid correlationId, ReadAllResult result, string error, ResolvedEvent[] events, 
                                                  int maxCount, TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfEofPosition)
            {
                Ensure.NotNull(events, "events");

                CorrelationId = correlationId;
                Result = result;
                Error = error;
                Events = events;
                MaxCount = maxCount;
                CurrentPos = currentPos;
                NextPos = nextPos;
                PrevPos = prevPos;
                TfEofPosition = tfEofPosition;
            }
        }

        public class SubscribeToStream : ReadRequestMessage
        {
            public readonly Guid ConnectionId;
            public readonly string EventStreamId; // should be empty to subscribe to all
            public readonly bool ResolveLinkTos;

            public SubscribeToStream(Guid correlationId, IEnvelope envelope, Guid connectionId, string eventStreamId,
                                     bool resolveLinkTos, IPrincipal user)
                : base(correlationId, envelope, user)
            {
                Ensure.NotEmptyGuid(connectionId, "connectionId");
                ConnectionId = connectionId;
                EventStreamId = eventStreamId;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        public class UnsubscribeFromStream : ReadRequestMessage
        {
            public UnsubscribeFromStream(Guid correlationId, IEnvelope envelope, IPrincipal user)
                : base(correlationId, envelope, user)
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
            public readonly SubscriptionDropReason Reason;

            public SubscriptionDropped(Guid correlationId, SubscriptionDropReason reason)
            {
                CorrelationId = correlationId;
                Reason = reason;
            }
        }
    }
}