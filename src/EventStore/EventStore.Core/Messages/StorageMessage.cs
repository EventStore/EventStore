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
            bool AllowImplicitStreamCreation { get; }
        }

        public interface IFlushableWriterMessage
        {
        }

        public class WritePrepares : Message, IPreconditionedWriteMessage, IFlushableWriterMessage
        {
            public Guid CorrelationId { get; private set; }
            public IEnvelope Envelope { get; private set; }
            public string EventStreamId { get; private set; }
            public int ExpectedVersion { get; private set; }
            public readonly Event[] Events;

            public bool AllowImplicitStreamCreation { get; private set; }
            public readonly DateTime LiveUntil;

            public WritePrepares(Guid correlationId, 
                                 IEnvelope envelope, 
                                 string eventStreamId, 
                                 int expectedVersion, 
                                 Event[] events, 
                                 bool allowImplicitStreamCreation,
                                 DateTime liveUntil)
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

                AllowImplicitStreamCreation = allowImplicitStreamCreation;
                LiveUntil = liveUntil;
            }
        }

        public class WriteDelete : Message, IPreconditionedWriteMessage, IFlushableWriterMessage
        {
            public Guid CorrelationId { get; private set; }
            public IEnvelope Envelope { get; private set; }
            public string EventStreamId { get; private set; }
            public int ExpectedVersion { get; private set; }

            public bool AllowImplicitStreamCreation { get; private set; }
            public readonly DateTime LiveUntil;

            public WriteDelete(Guid correlationId, 
                               IEnvelope envelope, 
                               string eventStreamId, 
                               int expectedVersion, 
                               bool allowImplicitStreamCreation, 
                               DateTime liveUntil)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");

                CorrelationId = correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;

                AllowImplicitStreamCreation = allowImplicitStreamCreation;
                LiveUntil = liveUntil;
            }
        }

        public class WriteCommit : Message, IFlushableWriterMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long PrepareStartPosition;

            public WriteCommit(Guid correlationId, IEnvelope envelope, long prepareStartPosition)
            {
                CorrelationId = correlationId;
                Envelope = envelope;
                PrepareStartPosition = prepareStartPosition;
            }
        }

        public class WriteTransactionStart : Message, IPreconditionedWriteMessage, IFlushableWriterMessage
        {
            public Guid CorrelationId { get; private set; }
            public IEnvelope Envelope { get; private set; }
            public string EventStreamId { get; private set; }
            public int ExpectedVersion { get; private set; }

            public bool AllowImplicitStreamCreation { get; private set; }
            public readonly DateTime LiveUntil;

            public WriteTransactionStart(Guid correlationId, 
                                         IEnvelope envelope, 
                                         string eventStreamId, 
                                         int expectedVersion, 
                                         bool allowImplicitStreamCreation,
                                         DateTime liveUntil)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNull(eventStreamId, "eventStreamId");

                CorrelationId = correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;

                AllowImplicitStreamCreation = allowImplicitStreamCreation;
                LiveUntil = liveUntil;
            }
        }

        public class WriteTransactionData : Message, IFlushableWriterMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long TransactionId;
            public readonly string EventStreamId;
            public readonly Event[] Events;

            public WriteTransactionData(Guid correlationId, 
                                        IEnvelope envelope, 
                                        long transactionId, 
                                        string eventStreamId, 
                                        Event[] events)
            {
                CorrelationId = correlationId;
                Envelope = envelope;
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                Events = events;
            }
        }

        public class WriteTransactionPrepare : Message, IFlushableWriterMessage
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long TransactionId;
            public readonly string EventStreamId;

            public readonly DateTime LiveUntil;

            public WriteTransactionPrepare(Guid correlationId, 
                                           IEnvelope envelope, 
                                           long transactionId, 
                                           string eventStreamId,
                                           DateTime liveUntil)
            {
                CorrelationId = correlationId;
                Envelope = envelope;
                TransactionId = transactionId;
                EventStreamId = eventStreamId;

                LiveUntil = liveUntil;
            }
        }

        public class PrepareAck : Message
        {
            public readonly Guid CorrelationId;
            public readonly long LogPosition;
            public readonly PrepareFlags Flags; 

            public PrepareAck(Guid correlationId, long logPosition, PrepareFlags flags)
            {
                CorrelationId = correlationId;
                LogPosition = logPosition;
                Flags = flags;
            }
        }

        public class CommitAck : Message
        {
            public readonly Guid CorrelationId;
            public readonly long StartPosition;
            public readonly int EventNumber;

            public CommitAck(Guid correlationId, long startPosition, int eventNumber)
            {
                Ensure.Nonnegative(startPosition, "startPosition");
                Ensure.Nonnegative(eventNumber, "eventNumber");

                CorrelationId = correlationId;
                StartPosition = startPosition;
                EventNumber = eventNumber;
            }
        }

        public class EventCommited: Message
        {
            public readonly long CommitPosition;
            public readonly int EventNumber;
            public readonly PrepareLogRecord Prepare;

            public EventCommited(long commitPosition, int eventNumber, PrepareLogRecord prepare)
            {
                CommitPosition = commitPosition;
                EventNumber = eventNumber;
                Prepare = prepare;
            }
        }

        public class CreateStreamRequestCreated : Message
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly string EventStreamId;
            public readonly byte[] Metadata;

            public CreateStreamRequestCreated(Guid correlationId,
                                              IEnvelope envelope,
                                              string eventStreamId,
                                              byte[] metadata)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                Ensure.NotNull(metadata, "metadata");

                CorrelationId = correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                Metadata = metadata;
            }
        }

        public class WriteRequestCreated : Message
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;

            public readonly string EventStreamId;
            public readonly int ExpectedVersion;

            public readonly Event[] Events;

            public WriteRequestCreated(Guid correlationId, 
                                       IEnvelope envelope,
                                       string eventStreamId,
                                       int expectedVersion,
                                       Event[] events)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                Ensure.NotNull(events, "events");

                CorrelationId = correlationId;
                Envelope = envelope;

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;

                Events = events;
            }
        }

        public class TransactionStartRequestCreated : Message
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly string EventStreamId;
            public readonly int ExpectedVersion;

            public TransactionStartRequestCreated(Guid correlationId, IEnvelope envelope, string eventStreamId, int expectedVersion)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");

                CorrelationId = correlationId;
                Envelope = envelope;
                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class TransactionWriteRequestCreated : Message
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long TransactionId;
            public readonly string EventStreamId;
            public readonly Event[] Events;

            public TransactionWriteRequestCreated(Guid correlationId, IEnvelope envelope, long transactionId, string eventStreamId, Event[] events)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.Nonnegative(transactionId, "transactionId");
                Ensure.NotNull(eventStreamId, "eventStreamId");
                Ensure.NotNull(events, "events");

                CorrelationId = correlationId;
                Envelope = envelope;
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
                Events = events;
            }
        }

        public class TransactionCommitRequestCreated : Message
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;
            public readonly long TransactionId;
            public readonly string EventStreamId;

            public TransactionCommitRequestCreated(Guid correlationId, IEnvelope envelope, long transactionId, string eventStreamId)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.Nonnegative(transactionId, "transactionId");
                Ensure.NotNull(eventStreamId, "eventStreamID");

                CorrelationId = correlationId;
                Envelope = envelope;
                TransactionId = transactionId;
                EventStreamId = eventStreamId;
            }
        }

        public class DeleteStreamRequestCreated : Message
        {
            public readonly Guid CorrelationId;
            public readonly IEnvelope Envelope;

            public readonly string EventStreamId;
            public readonly int ExpectedVersion;

            public DeleteStreamRequestCreated(Guid correlationId,
                                              IEnvelope envelope,
                                              string eventStreamId,
                                              int expectedVersion)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNull(envelope, "envelope");
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");

                CorrelationId = correlationId;
                Envelope = envelope;

                EventStreamId = eventStreamId;
                ExpectedVersion = expectedVersion;
            }
        }

        public class AlreadyCommitted: Message
        {
            public readonly Guid CorrelationId;

            public readonly string EventStreamId;
            public readonly int StartEventNumber;
            public readonly int EndEventNumber;

            public AlreadyCommitted(Guid correlationId, string eventStreamId, int startEventNumber, int endEventNumber)
            {
                Ensure.NotEmptyGuid(correlationId, "correlationId");
                Ensure.NotNullOrEmpty(eventStreamId, "eventStreamId");
                Ensure.Nonnegative(startEventNumber, "startEventNumber");
                if (endEventNumber < startEventNumber)
                    throw new ArgumentOutOfRangeException("endEventNumber", "EndEventNumber is less than StartEventNumber");

                CorrelationId = correlationId;
                EventStreamId = eventStreamId;
                StartEventNumber = startEventNumber;
                EndEventNumber = endEventNumber;
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

        public class PreparePhaseTimeout : Message
        {
            public readonly Guid CorrelationId;

            public PreparePhaseTimeout(Guid correlationId)
            {
                CorrelationId = correlationId;
            }
        }

        public class CommitPhaseTimeout : Message
        {
            public readonly Guid CorrelationId;

            public CommitPhaseTimeout(Guid correlationId)
            {
                CorrelationId = correlationId;
            }
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