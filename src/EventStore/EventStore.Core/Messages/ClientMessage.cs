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
using System.Threading;
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
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly bool ExitProcess;

            public RequestShutdown(bool exitProcess)
            {
                ExitProcess = exitProcess;
            }
        }

        public abstract class WriteRequestMessage : Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid InternalCorrId;
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly bool RequireMaster;

            public readonly IPrincipal User;
            public readonly string Login;
            public readonly string Password;

            protected WriteRequestMessage(Guid internalCorrId,
                                          Guid correlationId, IEnvelope envelope, bool requireMaster,
                                          IPrincipal user, string login, string password)
            {
                Ensure.NotEmptyGuid(internalCorrId, "internalCorrId");
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");

                InternalCorrId = internalCorrId;
                CorrelationId = correlationId;
                Envelope = envelope;
                RequireMaster = requireMaster;

                User = user;
                Login = login;
                Password = password;
            }
        }

        public abstract class ReadRequestMessage: Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid InternalCorrId;
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;

            public readonly IPrincipal User;

            protected ReadRequestMessage(Guid internalCorrId, Guid correlationId, IEnvelope envelope, IPrincipal user)
            {
                Ensure.NotEmptyGuid(internalCorrId, "internalCorrId");
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");

                InternalCorrId = internalCorrId;
                CorrelationId = correlationId;
                Envelope = envelope;

                User = user;
            }
        }

        public abstract class ReadResponseMessage : Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }
        }

        public class TcpForwardMessage: Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Message Message;

            public TcpForwardMessage(Message message)
            {
                Ensure.NotNull(message, "message");

                Message = message;
            }
        }

        public class NotHandled : Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

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
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string EventStreamId;
            public readonly int ExpectedVersion;
            public readonly Event[] Events;

            public WriteEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireMaster, 
                               string eventStreamId, int expectedVersion, Event[] events,
                               IPrincipal user, string login = null, string password = null)
                : base(internalCorrId, correlationId, envelope, requireMaster, user, login, password)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (expectedVersion < Data.ExpectedVersion.Any) throw new ArgumentOutOfRangeException("expectedVersion");
                Ensure.NotNull(events, "events");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                Events = events;
            }

            public WriteEvents(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireMaster,
                               string eventStreamId, int expectedVersion, Event @event,
                               IPrincipal user, string login = null, string password = null)
                : this(internalCorrId, correlationId, envelope, requireMaster, eventStreamId, expectedVersion,
                       @event == null ? null : new[] { @event }, user, login, password)
            {
            }

            public override string ToString()
            {
                return String.Format("WRITE: InternalCorrId: {0}, CorrelationId: {1}, EventStreamId: {2}, ExpectedVersion: {3}, Events: {4}",
                                     InternalCorrId, CorrelationId, EventStreamId, ExpectedVersion, Events.Length);
            }
        }

        public class WriteEventsCompleted : Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly OperationResult Result;
            public readonly string Message;
            public readonly int FirstEventNumber;
            public readonly int LastEventNumber;

            public WriteEventsCompleted(Guid correlationId, int firstEventNumber, int lastEventNumber)
            {
                if (firstEventNumber < -1)
                    throw new ArgumentOutOfRangeException("firstEventNumber", string.Format("FirstEventNumber: {0}", firstEventNumber));
                if (lastEventNumber - firstEventNumber + 1 < 0)
                    throw new ArgumentOutOfRangeException("lastEventNumber", string.Format("LastEventNumber {0}, FirstEventNumber {1}.", lastEventNumber, firstEventNumber));

                CorrelationId = correlationId;
                Result = OperationResult.Success;
                Message = null;
                FirstEventNumber = firstEventNumber;
                LastEventNumber = lastEventNumber;
            }

            public WriteEventsCompleted(Guid correlationId, OperationResult result, string message)
            {
                if (result == OperationResult.Success)
                    throw new ArgumentException("Invalid constructor used for successful write.", "result");

                CorrelationId = correlationId;
                Result = result;
                Message = message;
                FirstEventNumber = EventNumber.Invalid;
                LastEventNumber = EventNumber.Invalid;
            }

            private WriteEventsCompleted(Guid correlationId, OperationResult result, string message, int firstEventNumber, int lastEventNumber)
            {
                CorrelationId = correlationId;
                Result = result;
                Message = message;
                FirstEventNumber = firstEventNumber;
                LastEventNumber = lastEventNumber;
            }

            public WriteEventsCompleted WithCorrelationId(Guid newCorrId)
            {
                return new WriteEventsCompleted(newCorrId, Result, Message, FirstEventNumber, LastEventNumber);
            }

            public override string ToString()
            {
                return String.Format("WRITE COMPLETED: CorrelationId: {0}, Result: {1}, Message: {2}, FirstEventNumber: {3}, LastEventNumber: {4}",
                                     CorrelationId, Result, Message, FirstEventNumber, LastEventNumber);
            }
        }

        public class TransactionStart : WriteRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string EventStreamId;
            public readonly int ExpectedVersion;

            public TransactionStart(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireMaster,
                                    string eventStreamId, int expectedVersion,
                                    IPrincipal user, string login = null, string password = null)
                : base(internalCorrId, correlationId, envelope, requireMaster, user, login, password)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (expectedVersion < Data.ExpectedVersion.Any) throw new ArgumentOutOfRangeException("expectedVersion");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class TransactionStartCompleted : Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

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

            public TransactionStartCompleted WithCorrelationId(Guid newCorrId)
            {
                return new TransactionStartCompleted(newCorrId, TransactionId, Result, Message);
            }
        }

        public class TransactionWrite : WriteRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly long TransactionId;
            public readonly Event[] Events;

            public TransactionWrite(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireMaster,
                                    long transactionId, Event[] events,
                                    IPrincipal user, string login = null, string password = null)
                : base(internalCorrId, correlationId, envelope, requireMaster, user, login, password)
            {
                Ensure.Nonnegative(transactionId, "transactionId");
                Ensure.NotNull(events, "events");

                TransactionId = transactionId;
                Events = events;
            }
        }

        public class TransactionWriteCompleted : Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

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

            public TransactionWriteCompleted WithCorrelationId(Guid newCorrId)
            {
                return new TransactionWriteCompleted(newCorrId, TransactionId, Result, Message);
            }
        }

        public class TransactionCommit : WriteRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly long TransactionId;

            public TransactionCommit(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireMaster,
                                     long transactionId, IPrincipal user, string login = null, string password = null)
                : base(internalCorrId, correlationId, envelope, requireMaster, user, login, password)
            {
                Ensure.Nonnegative(transactionId, "transactionId");
                TransactionId = transactionId;
            }
        }

        public class TransactionCommitCompleted : Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly long TransactionId;
            public readonly OperationResult Result;
            public readonly string Message;
            public readonly int FirstEventNumber;
            public readonly int LastEventNumber;

            public TransactionCommitCompleted(Guid correlationId, long transactionId, int firstEventNumber, int lastEventNumber)
            {
                if (firstEventNumber < -1)
                    throw new ArgumentOutOfRangeException("firstEventNumber", string.Format("FirstEventNumber: {0}", firstEventNumber));
                if (lastEventNumber - firstEventNumber + 1 < 0)
                    throw new ArgumentOutOfRangeException("lastEventNumber", string.Format("LastEventNumber {0}, FirstEventNumber {1}.", lastEventNumber, firstEventNumber));
                CorrelationId = correlationId;
                TransactionId = transactionId;
                Result = OperationResult.Success;
                Message = string.Empty;
                FirstEventNumber = firstEventNumber;
                LastEventNumber = lastEventNumber;
            }

            public TransactionCommitCompleted(Guid correlationId, long transactionId, OperationResult result, string message)
            {
                if (result == OperationResult.Success)
                    throw new ArgumentException("Invalid constructor used for successful write.", "result");

                CorrelationId = correlationId;
                TransactionId = transactionId;
                Result = result;
                Message = message;
                FirstEventNumber = EventNumber.Invalid;
                LastEventNumber = EventNumber.Invalid;
            }

            private TransactionCommitCompleted(Guid correlationId, long transactionId, OperationResult result, string message,
                                               int firstEventNumber, int lastEventNumber)
            {
                CorrelationId = correlationId;
                TransactionId = transactionId;
                Result = result;
                Message = message;
                FirstEventNumber = firstEventNumber;
                LastEventNumber = lastEventNumber;
            }

            public TransactionCommitCompleted WithCorrelationId(Guid newCorrId)
            {
                return new TransactionCommitCompleted(newCorrId, TransactionId, Result, Message, FirstEventNumber, LastEventNumber);
            }
        }

        public class DeleteStream : WriteRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string EventStreamId;
            public readonly int ExpectedVersion;
            public readonly bool HardDelete;

            public DeleteStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope, bool requireMaster,
                                string eventStreamId, int expectedVersion, bool hardDelete,
                                IPrincipal user, string login = null, string password = null)
                : base(internalCorrId, correlationId, envelope, requireMaster, user, login, password)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (expectedVersion < Data.ExpectedVersion.Any) throw new ArgumentOutOfRangeException("expectedVersion");

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                HardDelete = hardDelete;
            }
        }

        public class DeleteStreamCompleted : Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly OperationResult Result;
            public readonly string Message;

            public DeleteStreamCompleted(Guid correlationId, OperationResult result, string message)
            {
                CorrelationId = correlationId;
                Result = result;
                Message = message;
            }

            public DeleteStreamCompleted WithCorrelationId(Guid newCorrId)
            {
                return new DeleteStreamCompleted(newCorrId, Result, Message);
            }
        }

        public class ReadEvent : ReadRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string EventStreamId;
            public readonly int EventNumber;
            public readonly bool ResolveLinkTos;
            public readonly bool RequireMaster;

            public ReadEvent(Guid internalCorrId, Guid correlationId, IEnvelope envelope, string eventStreamId, int eventNumber,
                             bool resolveLinkTos, bool requireMaster, IPrincipal user)
                : base(internalCorrId, correlationId, envelope, user)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (eventNumber < -1) throw new ArgumentOutOfRangeException("eventNumber");

                EventStreamId = eventStreamId;
                EventNumber = eventNumber;
                ResolveLinkTos = resolveLinkTos;
                RequireMaster = requireMaster;
            }
        }

        public class ReadEventCompleted : ReadResponseMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly ReadEventResult Result;
            public readonly ResolvedEvent Record;
            public readonly StreamMetadata StreamMetadata;
            public readonly bool IsCachePublic;
            public readonly string Error;

            public ReadEventCompleted(Guid correlationId, string eventStreamId, ReadEventResult result,
                                      ResolvedEvent record, StreamMetadata streamMetadata, bool isCachePublic, string error)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (result == ReadEventResult.Success)
                    Ensure.NotNull(record.Event, "record.Event");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                Result = result;
                Record = record;
                StreamMetadata = streamMetadata;
                IsCachePublic = isCachePublic;
                Error = error;
            }
        }

        public class ReadStreamEventsForward : ReadRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string EventStreamId;
            public readonly int FromEventNumber;
            public readonly int MaxCount;
            public readonly bool ResolveLinkTos;
            public readonly bool RequireMaster;

            public readonly int? ValidationStreamVersion;
            public readonly TimeSpan? LongPollTimeout;

            public ReadStreamEventsForward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
                                           string eventStreamId, int fromEventNumber, int maxCount, bool resolveLinkTos,
                                           bool requireMaster, int? validationStreamVersion, IPrincipal user,
                                           TimeSpan? longPollTimeout = null)
                : base(internalCorrId, correlationId, envelope, user)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (fromEventNumber < -1) throw new ArgumentOutOfRangeException("fromEventNumber");

                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;
                ResolveLinkTos = resolveLinkTos;
                RequireMaster = requireMaster;
                ValidationStreamVersion = validationStreamVersion;
                LongPollTimeout = longPollTimeout;
            }

            public override string ToString()
            {
                return String.Format(GetType().Name + " InternalCorrId: {0}, CorrelationId: {1}, EventStreamId: {2}, "
                                     + "FromEventNumber: {3}, MaxCount: {4}, ResolveLinkTos: {5}, RequireMaster: {6}, ValidationStreamVersion: {7}",
                                     InternalCorrId, CorrelationId, EventStreamId,
                                     FromEventNumber, MaxCount, ResolveLinkTos, RequireMaster, ValidationStreamVersion);
            }
        }

        public class ReadStreamEventsForwardCompleted : ReadResponseMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly int FromEventNumber;
            public readonly int MaxCount;
            
            public readonly ReadStreamResult Result;
            public readonly ResolvedEvent[] Events;
            public readonly StreamMetadata StreamMetadata;
            public readonly bool IsCachePublic;
            public readonly string Error;
            public readonly int NextEventNumber;
            public readonly int LastEventNumber;
            public readonly bool IsEndOfStream;
            public readonly long TfLastCommitPosition;

            public ReadStreamEventsForwardCompleted(Guid correlationId, string eventStreamId, int fromEventNumber, int maxCount,
                                                    ReadStreamResult result, ResolvedEvent[] events,
                                                    StreamMetadata streamMetadata, bool isCachePublic,
                                                    string error, int nextEventNumber, int lastEventNumber, bool isEndOfStream,
                                                    long tfLastCommitPosition)
            {
                Ensure.NotNull(events, "events");

                if (result != ReadStreamResult.Success)
                {
                    Ensure.Equal(nextEventNumber, -1, "nextEventNumber");
                    Ensure.Equal(isEndOfStream, true, "isEndOfStream");
                }

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;

                Result = result;
                Events = events;
                StreamMetadata = streamMetadata;
                IsCachePublic = isCachePublic;
                Error = error;
                NextEventNumber = nextEventNumber;
                LastEventNumber = lastEventNumber;
                IsEndOfStream = isEndOfStream;
                TfLastCommitPosition = tfLastCommitPosition;
            }
        }

        public class ReadStreamEventsBackward : ReadRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly string EventStreamId;
            public readonly int FromEventNumber;
            public readonly int MaxCount;
            public readonly bool ResolveLinkTos;
            public readonly bool RequireMaster;

            public readonly int? ValidationStreamVersion;

            public ReadStreamEventsBackward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
                                            string eventStreamId, int fromEventNumber, int maxCount, bool resolveLinkTos,
                                            bool requireMaster, int? validationStreamVersion, IPrincipal user)
                : base(internalCorrId, correlationId, envelope, user)
            {
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                if (fromEventNumber < -1) throw new ArgumentOutOfRangeException("fromEventNumber");

                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;
                ResolveLinkTos = resolveLinkTos;
                RequireMaster = requireMaster;
                ValidationStreamVersion = validationStreamVersion;
            }

            public override string ToString()
            {
                return String.Format(GetType().Name + " InternalCorrId: {0}, CorrelationId: {1}, EventStreamId: {2}, "
                                     + "FromEventNumber: {3}, MaxCount: {4}, ResolveLinkTos: {5}, RequireMaster: {6}, ValidationStreamVersion: {7}",
                                     InternalCorrId, CorrelationId, EventStreamId, FromEventNumber, MaxCount,
                                     ResolveLinkTos, RequireMaster, ValidationStreamVersion);
            }
        }

        public class ReadStreamEventsBackwardCompleted : ReadResponseMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly string EventStreamId;
            public readonly int FromEventNumber;
            public readonly int MaxCount;

            public readonly ReadStreamResult Result;
            public readonly ResolvedEvent[] Events;
            public readonly StreamMetadata StreamMetadata;
            public readonly bool IsCachePublic;
            public readonly string Error;
            public readonly int NextEventNumber;
            public readonly int LastEventNumber;
            public readonly bool IsEndOfStream;
            public readonly long TfLastCommitPosition;

            public ReadStreamEventsBackwardCompleted(Guid correlationId,
                                                     string eventStreamId,
                                                     int fromEventNumber,
                                                     int maxCount,
                                                     ReadStreamResult result,
                                                     ResolvedEvent[] events,
                                                     StreamMetadata streamMetadata,
                                                     bool isCachePublic,
                                                     string error,
                                                     int nextEventNumber,
                                                     int lastEventNumber,
                                                     bool isEndOfStream,
                                                     long tfLastCommitPosition)
            {
                Ensure.NotNull(events, "events");

                if (result != ReadStreamResult.Success)
                {
                    Ensure.Equal(nextEventNumber, -1, "nextEventNumber");
                    Ensure.Equal(isEndOfStream, true, "isEndOfStream");
                }

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                FromEventNumber = fromEventNumber;
                MaxCount = maxCount;

                Result = result;
                Events = events;
                StreamMetadata = streamMetadata;
                IsCachePublic = isCachePublic;
                Error = error;
                NextEventNumber = nextEventNumber;
                LastEventNumber = lastEventNumber;
                IsEndOfStream = isEndOfStream;
                TfLastCommitPosition = tfLastCommitPosition;
            }
        }

        public class ReadAllEventsForward : ReadRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly long CommitPosition;
            public readonly long PreparePosition;
            public readonly int MaxCount;
            public readonly bool ResolveLinkTos;
            public readonly bool RequireMaster;

            public readonly long? ValidationTfLastCommitPosition;
            public readonly TimeSpan? LongPollTimeout;

            public ReadAllEventsForward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
                                        long commitPosition, long preparePosition, int maxCount, bool resolveLinkTos,
                                        bool requireMaster, long? validationTfLastCommitPosition, IPrincipal user,
                                        TimeSpan? longPollTimeout = null)
                : base(internalCorrId, correlationId, envelope, user)
            {
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                MaxCount = maxCount;
                ResolveLinkTos = resolveLinkTos;
                RequireMaster = requireMaster;
                ValidationTfLastCommitPosition = validationTfLastCommitPosition;
                LongPollTimeout = longPollTimeout;
            }
        }

        public class ReadAllEventsForwardCompleted : ReadResponseMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;

            public readonly ReadAllResult Result;
            public readonly string Error;

            public readonly ResolvedEvent[] Events;
            public readonly StreamMetadata StreamMetadata;
            public readonly bool IsCachePublic;
            public readonly int MaxCount;
            public readonly TFPos CurrentPos;
            public readonly TFPos NextPos;
            public readonly TFPos PrevPos;
            public readonly long TfLastCommitPosition;

            public bool IsEndOfStream { get { return Events == null || Events.Length < MaxCount; } }

            public ReadAllEventsForwardCompleted(Guid correlationId, ReadAllResult result, string error, ResolvedEvent[] events,
                                                 StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
                                                 TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition)
            {
                Ensure.NotNull(events, "events");

                CorrelationId = correlationId;
                Result = result;
                Error = error;
                Events = events;
                StreamMetadata = streamMetadata;
                IsCachePublic = isCachePublic;
                MaxCount = maxCount;
                CurrentPos = currentPos;
                NextPos = nextPos;
                PrevPos = prevPos;
                TfLastCommitPosition = tfLastCommitPosition;
            }
        }

        public class ReadAllEventsBackward : ReadRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly long CommitPosition;
            public readonly long PreparePosition;
            public readonly int MaxCount;
            public readonly bool ResolveLinkTos;
            public readonly bool RequireMaster;

            public readonly long? ValidationTfLastCommitPosition;

            public ReadAllEventsBackward(Guid internalCorrId, Guid correlationId, IEnvelope envelope,
                                         long commitPosition, long preparePosition, int maxCount, bool resolveLinkTos,
                                         bool requireMaster, long? validationTfLastCommitPosition, IPrincipal user)
                : base(internalCorrId, correlationId, envelope, user)
            {
                CommitPosition = commitPosition;
                PreparePosition = preparePosition;
                MaxCount = maxCount;
                ResolveLinkTos = resolveLinkTos;
                RequireMaster = requireMaster;
                ValidationTfLastCommitPosition = validationTfLastCommitPosition;
            }
        }

        public class ReadAllEventsBackwardCompleted : ReadResponseMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;

            public readonly ReadAllResult Result;
            public readonly string Error;

            public readonly ResolvedEvent[] Events;
            public readonly StreamMetadata StreamMetadata;
            public readonly bool IsCachePublic;
            public readonly int MaxCount;
            public readonly TFPos CurrentPos;
            public readonly TFPos NextPos;
            public readonly TFPos PrevPos;
            public readonly long TfLastCommitPosition;

            public bool IsEndOfStream { get { return Events == null || Events.Length < MaxCount; } }

            public ReadAllEventsBackwardCompleted(Guid correlationId, ReadAllResult result, string error, ResolvedEvent[] events, 
                                                  StreamMetadata streamMetadata, bool isCachePublic, int maxCount,
                                                  TFPos currentPos, TFPos nextPos, TFPos prevPos, long tfLastCommitPosition)
            {
                Ensure.NotNull(events, "events");

                CorrelationId = correlationId;
                Result = result;
                Error = error;
                Events = events;
                StreamMetadata = streamMetadata;
                IsCachePublic = isCachePublic;
                MaxCount = maxCount;
                CurrentPos = currentPos;
                NextPos = nextPos;
                PrevPos = prevPos;
                TfLastCommitPosition = tfLastCommitPosition;
            }
        }

        public class SubscribeToStream : ReadRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid ConnectionId;
            public readonly string EventStreamId; // should be empty to subscribe to all
            public readonly bool ResolveLinkTos;

            public SubscribeToStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope, Guid connectionId,
                                     string eventStreamId, bool resolveLinkTos, IPrincipal user)
                : base(internalCorrId, correlationId, envelope, user)
            {
                Ensure.NotEmptyGuid(connectionId, "connectionId");
                ConnectionId = connectionId;
                EventStreamId = eventStreamId;
                ResolveLinkTos = resolveLinkTos;
            }
        }

        public class UnsubscribeFromStream : ReadRequestMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public UnsubscribeFromStream(Guid internalCorrId, Guid correlationId, IEnvelope envelope, IPrincipal user)
                : base(internalCorrId, correlationId, envelope, user)
            {
            }
        }

        public class SubscriptionConfirmation : Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

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
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

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
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly SubscriptionDropReason Reason;

            public SubscriptionDropped(Guid correlationId, SubscriptionDropReason reason)
            {
                CorrelationId = correlationId;
                Reason = reason;
            }
        }

        public class ScavengeDatabase: Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly IEnvelope Envelope;
            public readonly Guid CorrelationId;
            public readonly IPrincipal User;

            public ScavengeDatabase(IEnvelope envelope, Guid correlationId, IPrincipal user)
            {
                Ensure.NotNull(envelope, "envelope");
                Envelope = envelope;
                CorrelationId = correlationId;
                User = user;
            }

            public enum ScavengeResult
            {
                Success,
                InProgress,
                Failed
            }
        }

        public class ScavengeDatabaseCompleted: Message
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public readonly Guid CorrelationId;
            public readonly ScavengeDatabase.ScavengeResult Result;
            public readonly string Error;
            public readonly TimeSpan TotalTime;
            public readonly long TotalSpaceSaved;

            public ScavengeDatabaseCompleted(Guid correlationId,
                                             ScavengeDatabase.ScavengeResult result,
                                             string error,
                                             TimeSpan totalTime,
                                             long totalSpaceSaved)
            {
                CorrelationId = correlationId;
                Result = result;
                Error = error;
                TotalTime = totalTime;
                TotalSpaceSaved = totalSpaceSaved;
            }

            public override string ToString()
            {
                return string.Format("Result: {0}, Error: {1}, TotalTime: {2}, TotalSpaceSaved: {3}", Result, Error, TotalTime, TotalSpaceSaved);
            }
        }
    }
}