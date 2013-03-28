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
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Messages
{
    public static class StorageMessage
    {
        public interface IPreconditionedWriteMessage
        {
            Guid CorrelationId { get; }
            IEnvelope Envelope { get; }
            string EventStreamId { get; }
            int ExpectedVersion { get; }
        }

        public interface IFlushableMessage
        {
        }

        public interface IMasterWriteMessage
        {
             
        }

        public class WritePrepares : Message, IPreconditionedWriteMessage, IFlushableMessage, IMasterWriteMessage
        {
            public Guid CorrelationId { get; private set; }
            public IEnvelope Envelope { get; private set; }
            public string EventStreamId { get; private set; }
            public int ExpectedVersion { get; private set; }
            public readonly Event[] Events;

            public readonly DateTime LiveUntil;

            public WritePrepares(Guid correlationId, IEnvelope envelope, string eventStreamId, int expectedVersion, Event[] events, DateTime liveUntil)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");
                Ensure.NotNull(events, "events");
                
                CorrelationId = correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
                Events = events;

                LiveUntil = liveUntil;
            }
        }

        public class WriteDelete : Message, IPreconditionedWriteMessage, IFlushableMessage, IMasterWriteMessage
        {
            public Guid CorrelationId { get; private set; }
            public IEnvelope Envelope { get; private set; }
            public string EventStreamId { get; private set; }
            public int ExpectedVersion { get; private set; }

            public readonly DateTime LiveUntil;

            public WriteDelete(Guid correlationId, IEnvelope envelope, string eventStreamId, int expectedVersion, DateTime liveUntil)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");

                CorrelationId = correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;

                LiveUntil = liveUntil;
            }
        }

        public class WriteCommit : Message, IFlushableMessage, IMasterWriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long TransactionPosition;

            public WriteCommit(Guid correlationId, IEnvelope envelope, long transactionPosition)
            {
                CorrelationId = correlationId;
                Envelope = envelope;
                TransactionPosition = transactionPosition;
            }
        }

        public class WriteTransactionStart : Message, IPreconditionedWriteMessage, IFlushableMessage, IMasterWriteMessage
        {
            public Guid CorrelationId { get; private set; }
            public IEnvelope Envelope { get; private set; }
            public string EventStreamId { get; private set; }
            public int ExpectedVersion { get; private set; }

            public readonly DateTime LiveUntil;

            public WriteTransactionStart(Guid correlationId, IEnvelope envelope, string eventStreamId, int expectedVersion, DateTime liveUntil)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");

                CorrelationId = correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;

                LiveUntil = liveUntil;
            }
        }

        public class WriteTransactionData : Message, IFlushableMessage, IMasterWriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long TransactionId;
            public readonly Event[] Events;

            public WriteTransactionData(Guid correlationId, IEnvelope envelope, long transactionId, Event[] events)
            {
                CorrelationId = correlationId;
                Envelope = envelope;
                TransactionId = transactionId;
                Events = events;
            }
        }

        public class WriteTransactionPrepare : Message, IFlushableMessage, IMasterWriteMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long TransactionId;

            public readonly DateTime LiveUntil;

            public WriteTransactionPrepare(Guid correlationId, IEnvelope envelope, long transactionId, DateTime liveUntil)
            {
                CorrelationId = correlationId;
                Envelope = envelope;
                TransactionId = transactionId;

                LiveUntil = liveUntil;
            }
        }

        public class PrepareAck : Message
        {
            public readonly Guid CorrelationId;
            public readonly IPEndPoint VNodeEndPoint;
            public readonly long LogPosition;
            public readonly PrepareFlags Flags;

            public PrepareAck(Guid correlationId, IPEndPoint vnodeEndPoint, long logPosition, PrepareFlags flags)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(vnodeEndPoint, "vnodeEndPoint");
                Ensure.Nonnegative(logPosition, "logPosition");

                CorrelationId = correlationId;
                VNodeEndPoint = vnodeEndPoint;
                LogPosition = logPosition;
                Flags = flags;
            }
        }

        public class CommitAck : Message
        {
            public readonly Guid CorrelationId;
            public readonly IPEndPoint VNodeEndPoint;
            public readonly long LogPosition;
            public readonly long TransactionPosition;
            public readonly int FirstEventNumber;

            public CommitAck(Guid correlationId, IPEndPoint vnodeEndPoint, long logPosition, long transactionPosition, int firstEventNumber)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(vnodeEndPoint, "vnodeEndPoint");
                Ensure.Nonnegative(logPosition, "logPosition");
                Ensure.Nonnegative(transactionPosition, "transactionPosition");
                Ensure.Nonnegative(firstEventNumber, "firstEventNumber");

                CorrelationId = correlationId;
                VNodeEndPoint = vnodeEndPoint;
                LogPosition = logPosition;
                TransactionPosition = transactionPosition;
                FirstEventNumber = firstEventNumber;
            }
        }

        public class EventCommited: Message
        {
            public readonly long CommitPosition;
            public readonly EventRecord Event;

            public EventCommited(long commitPosition, EventRecord @event)
            {
                CommitPosition = commitPosition;
                Event = @event;
            }
        }

        public class AlreadyCommitted: Message
        {
            public readonly Guid CorrelationId;

            public readonly string EventStreamId;
            public readonly int FirstEventNumber;
            public readonly int LastEventNumber;

            public AlreadyCommitted(Guid correlationId, string eventStreamId, int firstEventNumber, int lastEventNumber)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                Ensure.Nonnegative(firstEventNumber, "FirstEventNumber");
                if (lastEventNumber < firstEventNumber)
                    throw new ArgumentOutOfRangeException("lastEventNumber", "LastEventNumber is less than FirstEventNumber");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                FirstEventNumber = firstEventNumber;
                LastEventNumber = lastEventNumber;
            }
        }

        public class InvalidTransaction : Message
        {
            public readonly Guid CorrelationId;

            public InvalidTransaction(Guid correlationId)
            {
                CorrelationId = correlationId;
            }
        }

        public class WrongExpectedVersion : Message
        {
            public readonly Guid CorrelationId;

            public WrongExpectedVersion(Guid correlationId)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                CorrelationId = correlationId;
            }
        }

        public class StreamDeleted : Message
        {
            public readonly Guid CorrelationId;

            public StreamDeleted(Guid correlationId)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                CorrelationId = correlationId;
            }
        }

        public class RequestCompleted : Message
        {
            public readonly Guid CorrelationId;
            public readonly bool Success;

            public RequestCompleted(Guid correlationId, bool success)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                CorrelationId = correlationId;
                Success = success;
            }
        }

        public class RequestManagerTimerTick: Message
        {
        }

        public class ForwardingTimeout : Message
        {
            public readonly Guid ForwardingId;
            public readonly Guid CorrelationId;
            public readonly Message TimeoutMessage;

            public ForwardingTimeout(Guid forwardingId, Guid correlationId, Message timeoutMessage)
            {
                Ensure.NotNull(timeoutMessage, "timeoutMessage");

                ForwardingId = forwardingId;
                CorrelationId = correlationId;
                TimeoutMessage = timeoutMessage;
            }
        }
    }
}