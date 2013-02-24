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
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    public static class ProjectionSubscriptionManagement
    {
        public class Subscribe : Message
        {
            private readonly Guid _correlationId;
            private readonly Guid _subscriptionId;
            private readonly ICoreProjection _subscriber;
            private readonly CheckpointTag _fromPosition;
            private readonly CheckpointStrategy _checkpointStrategy;
            private readonly long _checkpointUnhandledBytesThreshold;
            private readonly int _checkpointProcessedEventsThreshold; 
            private readonly bool _stopOnEof;

            public Subscribe(
                Guid correlationId, Guid subscriptionId, ICoreProjection subscriber, CheckpointTag from,
                CheckpointStrategy checkpointStrategy, long checkpointUnhandledBytesThreshold,
                int checkpointProcessedEventsThreshold, bool stopOnEof = false)
            {
                if (subscriber == null) throw new ArgumentNullException("subscriber");
                if (@from == null) throw new ArgumentNullException("from");
                if (checkpointStrategy == null) throw new ArgumentNullException("checkpointStrategy");
                _correlationId = correlationId;
                _subscriptionId = subscriptionId;
                _subscriber = subscriber;
                _fromPosition = @from;
                _checkpointStrategy = checkpointStrategy;
                _checkpointUnhandledBytesThreshold = checkpointUnhandledBytesThreshold;
                _checkpointProcessedEventsThreshold = checkpointProcessedEventsThreshold;
                _stopOnEof = stopOnEof;
            }

            public ICoreProjection Subscriber
            {
                get { return _subscriber; }
            }

            public CheckpointTag FromPosition
            {
                get { return _fromPosition; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

            public CheckpointStrategy CheckpointStrategy
            {
                get { return _checkpointStrategy; }
            }

            public long CheckpointUnhandledBytesThreshold
            {
                get { return _checkpointUnhandledBytesThreshold; }
            }

            public int CheckpointProcessedEventsThreshold 
            {
                get { return _checkpointProcessedEventsThreshold; }
            }

            public bool StopOnEof
            {
                get { return _stopOnEof; }
            }

            public Guid SubscriptionId
            {
                get { return _subscriptionId; }
            }
        }

        public class Pause : Message
        {
            private readonly Guid _correlationId;

            public Pause(Guid correlationId)
            {
                _correlationId = correlationId;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class Resume : Message
        {
            private readonly Guid _correlationId;

            public Resume(Guid correlationId)
            {
                _correlationId = correlationId;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class Unsubscribe : Message
        {
            private readonly Guid _correlationId;

            public Unsubscribe(Guid correlationId)
            {
                _correlationId = correlationId;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class ReaderAssigned : Message
        {
            private readonly Guid _correlationId;
            private readonly Guid _readerId;

            public ReaderAssigned(Guid correlationId, Guid readerId)
            {
                _correlationId = correlationId;
                _readerId = readerId;
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

            public Guid ReaderId
            {
                get { return _readerId; }
            }
        }
    }
}
