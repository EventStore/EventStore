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
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Messages
{
    public static class ReaderSubscriptionMessage
    {
        public class SubscriptionMessage : Message
        {
            private readonly Guid _correlationId;

            public SubscriptionMessage(Guid correlationId)
            {
                _correlationId = correlationId;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class EventReaderIdle : SubscriptionMessage
        {
            private readonly DateTime _idleTimestampUtc;

            public EventReaderIdle(Guid correlationId, DateTime idleTimestampUtc): base(correlationId)
            {
                _idleTimestampUtc = idleTimestampUtc;
            }

            public DateTime IdleTimestampUtc
            {
                get { return _idleTimestampUtc; }
            }
        }

        public class EventReaderEof : SubscriptionMessage
        {
            private readonly bool _maxEventsReached;

            public EventReaderEof(Guid correlationId, bool maxEventsReached = false)
                : base(correlationId)
            {
                _maxEventsReached = maxEventsReached;
            }

            public bool MaxEventsReached
            {
                get { return _maxEventsReached; }
            }
        }

        public class CommittedEventDistributed : SubscriptionMessage
        {
            public static CommittedEventDistributed Sample(
                Guid correlationId, TFPos position, string positionStreamId, int positionSequenceNumber,
                string eventStreamId, int eventSequenceNumber, bool resolvedLinkTo, Guid eventId, string eventType,
                bool isJson, byte[] data, byte[] metadata, long? safeTransactionFileReaderJoinPosition, float progress)
            {
                return new CommittedEventDistributed(
                    correlationId,
                    new ResolvedEvent(
                        positionStreamId, positionSequenceNumber, eventStreamId, eventSequenceNumber, resolvedLinkTo,
                        position, eventId, eventType, isJson, data, metadata, null, default(DateTime)),
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
                        eventId, eventType, isJson, data, metadata, null, timestamp.GetValueOrDefault()),
                    position.PreparePosition, 11.1f);
            }

            private readonly ResolvedEvent _data;

            private readonly long? _safeTransactionFileReaderJoinPosition;
            private readonly float _progress;

            //NOTE: committed event with null event _data means - end of the source reached.  
            // Current last available TF commit position is in _position.CommitPosition
            // TODO: separate message?

            public CommittedEventDistributed(
                Guid correlationId, ResolvedEvent data, long? safeTransactionFileReaderJoinPosition, float progress): base(correlationId)
            {
                _data = data;
                _safeTransactionFileReaderJoinPosition = safeTransactionFileReaderJoinPosition;
                _progress = progress;
            }

            public CommittedEventDistributed(Guid correlationId, ResolvedEvent data)
                : this(correlationId, data, data.Position.PreparePosition, 11.1f)
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
