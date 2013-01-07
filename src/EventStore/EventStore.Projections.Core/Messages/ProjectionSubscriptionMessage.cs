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
    public abstract class ProjectionSubscriptionMessage : Message
    {
        private readonly Guid _correlationId;
        private readonly Guid _subscriptionId;
        private readonly long _subscriptionMessageSequenceNumber;
        private readonly CheckpointTag _checkpointTag;
        private readonly float _progress;

        private ProjectionSubscriptionMessage(
            Guid correlationId, Guid subscriptionId, CheckpointTag checkpointTag, float progress,
            long subscriptionMessageSequenceNumber)
        {
            _correlationId = correlationId;
            _subscriptionId = subscriptionId;
            _checkpointTag = checkpointTag;
            _progress = progress;
            _subscriptionMessageSequenceNumber = subscriptionMessageSequenceNumber;
        }

        /// <summary>
        /// A ChechpointSuggested message is sent to core projection 
        /// to allow bookmarking a position that can be used to 
        /// restore the projection processing (typically
        /// an event at this position does not satisfy projection filter)
        /// </summary>
        public class CheckpointSuggested : ProjectionSubscriptionMessage
        {
            public CheckpointSuggested(
                Guid correlationId, Guid subscriptionId, CheckpointTag checkpointTag, float progress,
                long subscriptionMessageSequenceNumber)
                : base(correlationId, subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber)
            {
            }
        }

        public class ProgressChanged : ProjectionSubscriptionMessage
        {
            public ProgressChanged(
                Guid correlationId, Guid subscriptionId, CheckpointTag checkpointTag, float progress,
                long subscriptionMessageSequenceNumber)
                : base(correlationId, subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber)
            {
            }
        }

        public class EofReached : ProjectionSubscriptionMessage
        {
            public EofReached(
                Guid correlationId, Guid subscriptionId, CheckpointTag checkpointTag, float progress,
                long subscriptionMessageSequenceNumber)
                : base(correlationId, subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber)
            {
            }
        }

        public class CommittedEventReceived : ProjectionSubscriptionMessage
        {
            public static CommittedEventReceived Sample(
                Guid correlationId, Guid subscriptionId, EventPosition position, string eventStreamId,
                int eventSequenceNumber, bool resolvedLinkTo, ResolvedEvent data, long subscriptionMessageSequenceNumber)
            {
                return new CommittedEventReceived(
                    correlationId, subscriptionId, position, eventStreamId, eventSequenceNumber, null, resolvedLinkTo, data, 77.7f,
                    subscriptionMessageSequenceNumber);
            }

            private readonly ResolvedEvent _data;
            private readonly string _eventStreamId;
            private readonly int _eventSequenceNumber;
            private readonly bool _resolvedLinkTo;
            private readonly string _positionStreamId;
            private readonly int _positionSequenceNumber;
            private readonly string _eventCategory;
            private readonly EventPosition _position;

            private CommittedEventReceived(
                Guid correlationId, Guid subscriptionId, EventPosition position, CheckpointTag checkpointTag,
                string positionStreamId, int positionSequenceNumber, string eventStreamId, int eventSequenceNumber,
                string eventCategory, bool resolvedLinkTo, ResolvedEvent data, float progress,
                long subscriptionMessageSequenceNumber)
                : base(correlationId, subscriptionId, checkpointTag, progress, subscriptionMessageSequenceNumber)
            {
                if (data == null) throw new ArgumentNullException("data");
                _data = data;
                _position = position;
                _positionStreamId = positionStreamId;
                _positionSequenceNumber = positionSequenceNumber;
                _eventStreamId = eventStreamId;
                _eventSequenceNumber = eventSequenceNumber;
                _eventCategory = eventCategory;
                _resolvedLinkTo = resolvedLinkTo;
            }

            private CommittedEventReceived(
                Guid correlationId, Guid subscriptionId, EventPosition position, string eventStreamId,
                int eventSequenceNumber, string eventCategory, bool resolvedLinkTo, ResolvedEvent data, float progress,
                long subscriptionMessageSequenceNumber)
                : this(
                    correlationId, subscriptionId, position,
                    CheckpointTag.FromPosition(position.CommitPosition, position.PreparePosition), eventStreamId,
                    eventSequenceNumber, eventStreamId, eventSequenceNumber, eventCategory, resolvedLinkTo, data,
                    progress, subscriptionMessageSequenceNumber)
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

            public string EventCategory
            {
                get { return _eventCategory; }
            }

            public static CommittedEventReceived FromCommittedEventDistributed(
                ProjectionCoreServiceMessage.CommittedEventDistributed message, CheckpointTag checkpointTag,
                string eventCategory, Guid subscriptionId, long subscriptionMessageSequenceNumber)
            {
                return new CommittedEventReceived(
                    message.CorrelationId, subscriptionId, message.Position, checkpointTag, message.PositionStreamId,
                    message.PositionSequenceNumber, message.EventStreamId, message.EventSequenceNumber, eventCategory,
                    message.ResolvedLinkTo, message.Data, message.Progress, subscriptionMessageSequenceNumber);
            }
        }

        public Guid CorrelationId
        {
            get { return _correlationId; }
        }

        public CheckpointTag CheckpointTag
        {
            get { return _checkpointTag; }
        }

        public float Progress
        {
            get { return _progress; }
        }

        public long SubscriptionMessageSequenceNumber
        {
            get { return _subscriptionMessageSequenceNumber; }
        }

        public Guid SubscriptionId
        {
            get { return _subscriptionId; }
        }
    }
}
