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
using EventStore.Core.Data;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Messages
{
    public static class ReaderSubscriptionMessage
    {
        public class SubscriptionMessage : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly Guid _correlationId;
            private readonly CheckpointTag _preTagged;
            private readonly object _source;

            public SubscriptionMessage(Guid correlationId, CheckpointTag preTagged, object source)
            {
                _correlationId = correlationId;
                _preTagged = preTagged;
                _source = source;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

            public CheckpointTag PreTagged
            {
                get { return _preTagged; }
            }

            public object Source
            {
                get { return _source; }
            }
        }

        public class EventReaderIdle : SubscriptionMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly DateTime _idleTimestampUtc;

            public EventReaderIdle(Guid correlationId, DateTime idleTimestampUtc, object source = null)
                : base(correlationId, null, source)
            {
                _idleTimestampUtc = idleTimestampUtc;
            }

            public DateTime IdleTimestampUtc
            {
                get { return _idleTimestampUtc; }
            }
        }

        public sealed class EventReaderStarting : SubscriptionMessage
        {
            private readonly long _lastCommitPosition;
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public EventReaderStarting(Guid correlationId, long lastCommitPosition, object source = null)
                : base(correlationId, null, source)
            {
                _lastCommitPosition = lastCommitPosition;
            }

            public long LastCommitPosition
            {
                get { return _lastCommitPosition; }
            }
        }

        public class EventReaderEof : SubscriptionMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly bool _maxEventsReached;

            public EventReaderEof(Guid correlationId, bool maxEventsReached = false, object source = null)
                : base(correlationId, null, source)
            {
                _maxEventsReached = maxEventsReached;
            }

            public bool MaxEventsReached
            {
                get { return _maxEventsReached; }
            }
        }

        public class EventReaderPartitionEof : SubscriptionMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly string _partition;

            public EventReaderPartitionEof(
                Guid correlationId, string partition, CheckpointTag preTagged, object source = null)
                : base(correlationId, preTagged, source)
            {
                _partition = partition;
            }

            public string Partition
            {
                get { return _partition; }
            }
        }

        public class EventReaderPartitionDeleted : SubscriptionMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly string _partition;
            private readonly int? _lastEventNumber;
            private readonly TFPos? _deleteLinkOrEventPosition;
            private readonly TFPos? _deleteEventOrLinkTargetPosition;
            private readonly string _positionStreamId;
            private readonly int? _positionEventNumber;

            public EventReaderPartitionDeleted(
                Guid correlationId, string partition, int? lastEventNumber, TFPos? deleteLinkOrEventPosition,
                TFPos? deleteEventOrLinkTargetPosition, string positionStreamId, int? positionEventNumber,
                CheckpointTag preTagged = null, object source = null)
                : base(correlationId, preTagged, source)
            {
                _partition = partition;
                _lastEventNumber = lastEventNumber;
                _deleteLinkOrEventPosition = deleteLinkOrEventPosition;
                _deleteEventOrLinkTargetPosition = deleteEventOrLinkTargetPosition;
                _positionStreamId = positionStreamId;
                _positionEventNumber = positionEventNumber;
            }

            public string Partition
            {
                get { return _partition; }
            }

            public int? LastEventNumber
            {
                get { return _lastEventNumber; }
            }

            public TFPos? DeleteEventOrLinkTargetPosition
            {
                get { return _deleteEventOrLinkTargetPosition; }
            }

            public string PositionStreamId
            {
                get { return _positionStreamId; }
            }

            public int? PositionEventNumber
            {
                get { return _positionEventNumber; }
            }

            public TFPos? DeleteLinkOrEventPosition
            {
                get { return _deleteLinkOrEventPosition; }
            }
        }

        public class EventReaderPartitionMeasured : SubscriptionMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly string _partition;
            private readonly int _size;

            public EventReaderPartitionMeasured(
                Guid correlationId, string partition, int size, object source = null)
                : base(correlationId, null, source)
            {
                _partition = partition;
                _size = size;
            }

            public string Partition
            {
                get { return _partition; }
            }

            public int Size
            {
                get { return _size; }
            }
        }

        public sealed class EventReaderNotAuthorized : SubscriptionMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public EventReaderNotAuthorized(Guid correlationId, object source = null)
                : base(correlationId, null, source)
            {
            }
        }

        public class CommittedEventDistributed : SubscriptionMessage
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public static CommittedEventDistributed Sample(
                Guid correlationId, TFPos position, TFPos originalPosition, string positionStreamId, int positionSequenceNumber,
                string eventStreamId, int eventSequenceNumber, bool resolvedLinkTo, Guid eventId, string eventType,
                bool isJson, byte[] data, byte[] metadata, long? safeTransactionFileReaderJoinPosition, float progress)
            {
                return new CommittedEventDistributed(
                    correlationId,
                    new ResolvedEvent(
                        positionStreamId, positionSequenceNumber, eventStreamId, eventSequenceNumber, resolvedLinkTo,
                        position, originalPosition, eventId, eventType, isJson, data, metadata, null, null, default(DateTime)),
                    safeTransactionFileReaderJoinPosition, progress);
            }

            public static CommittedEventDistributed Sample(
                Guid correlationId, TFPos position, string eventStreamId, int eventSequenceNumber,
                bool resolvedLinkTo, Guid eventId, string eventType, bool isJson, byte[] data, byte[] metadata,
                DateTime? timestamp = null)
            {
                return new CommittedEventDistributed(
                    correlationId,
                    new ResolvedEvent(
                        eventStreamId, eventSequenceNumber, eventStreamId, eventSequenceNumber, resolvedLinkTo, position,
                        position, eventId, eventType, isJson, data, metadata, null, null, timestamp.GetValueOrDefault()),
                    position.PreparePosition, 11.1f);
            }

            private readonly ResolvedEvent _data;
            private readonly long? _safeTransactionFileReaderJoinPosition;
            private readonly float _progress;

            //NOTE: committed event with null event _data means - end of the source reached.  
            // Current last available TF commit position is in _position.CommitPosition
            // TODO: separate message?

            public CommittedEventDistributed(
                Guid correlationId, ResolvedEvent data, long? safeTransactionFileReaderJoinPosition, float progress,
                object source = null, CheckpointTag preTagged = null)
                : base(correlationId, preTagged, source)
            {
                _data = data;
                _safeTransactionFileReaderJoinPosition = safeTransactionFileReaderJoinPosition;
                _progress = progress;
            }

            public CommittedEventDistributed(Guid correlationId, ResolvedEvent data, CheckpointTag preTagged = null)
                : this(correlationId, data, data.Position.PreparePosition, 11.1f, preTagged)
            {
            }

            public ResolvedEvent Data
            {
                get { return _data; }
            }

            public long? SafeTransactionFileReaderJoinPosition
            {
                get { return _safeTransactionFileReaderJoinPosition; }
            }

            public float Progress
            {
                get { return _progress; }
            }

        }

    }
}
