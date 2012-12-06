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
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    public static class ProjectionCoreServiceMessage
    {
        public class Start : Message
        {
        }

        public class Stop : Message
        {
        }

        public class Connected : Message
        {
            private readonly TcpConnectionManager _connection;

            public Connected(TcpConnectionManager connection)
            {
                _connection = connection;
            }

            public TcpConnectionManager Connection
            {
                get { return _connection; }
            }
        }

        public class Tick : Message
        {
            private readonly Action _action;

            public Tick(Action action)
            {
                _action = action;
            }

            public Action Action
            {
                get { return _action; }
            }
        }

        public class EventReaderIdle : Message
        {
            private readonly Guid _correlationId;
            private readonly DateTime _idleTimestampUtc;

            public EventReaderIdle(Guid correlationId, DateTime idleTimestampUtc)
            {
                _correlationId = correlationId;
                _idleTimestampUtc = idleTimestampUtc;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

            public DateTime IdleTimestampUtc
            {
                get { return _idleTimestampUtc; }
            }
        }

        public class EventReaderEof : Message
        {
            private readonly Guid _correlationId;

            public EventReaderEof(Guid correlationId)
            {
                _correlationId = correlationId;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

        }

        public class CommittedEventDistributed : Message
        {
            private readonly Guid _correlationId;
            private readonly ResolvedEvent _data;
            private readonly string _eventStreamId;
            private readonly int _eventSequenceNumber;
            private readonly bool _resolvedLinkTo;
            private readonly string _positionStreamId;
            private readonly int _positionSequenceNumber;
            private readonly EventPosition _position;
            private readonly long? _safeTransactionFileReaderJoinPosition;
            private readonly float _progress;

            //NOTE: committed event with null event _data means - end of the source reached.  
            // Current last available TF commit position is in _position.CommitPosition
            // TODO: separate message?

            public CommittedEventDistributed(
                Guid correlationId, EventPosition position, string positionStreamId, int positionSequenceNumber,
                string eventStreamId, int eventSequenceNumber, bool resolvedLinkTo, ResolvedEvent data,
                long? safeTransactionFileReaderJoinPosition, float progress)
            {
                _correlationId = correlationId;
                _data = data;
                _safeTransactionFileReaderJoinPosition = safeTransactionFileReaderJoinPosition;
                _progress = progress;
                _position = position;
                _positionStreamId = positionStreamId;
                _positionSequenceNumber = positionSequenceNumber;
                _eventStreamId = eventStreamId;
                _eventSequenceNumber = eventSequenceNumber;
                _resolvedLinkTo = resolvedLinkTo;
            }

            public CommittedEventDistributed(
                Guid correlationId, EventPosition position, string eventStreamId, int eventSequenceNumber,
                bool resolvedLinkTo, ResolvedEvent data)
                : this(
                    correlationId, position, eventStreamId, eventSequenceNumber, eventStreamId, eventSequenceNumber,
                    resolvedLinkTo, data, position.PreparePosition, 11.1f)
            {
            }

            public ResolvedEvent Data
            {
                get { return _data; }
            }

            public EventPosition Position
            {
                get { return _position; }
            }

            public string EventStreamId
            {
                get { return _eventStreamId; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

            public int EventSequenceNumber
            {
                get { return _eventSequenceNumber; }
            }

            public string PositionStreamId
            {
                get { return _positionStreamId; }
            }

            public int PositionSequenceNumber
            {
                get { return _positionSequenceNumber; }
            }

            public bool ResolvedLinkTo
            {
                get { return _resolvedLinkTo; }
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
