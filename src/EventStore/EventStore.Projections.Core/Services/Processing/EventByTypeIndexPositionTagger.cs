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
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EventByTypeIndexPositionTagger : PositionTagger
    {
        private readonly HashSet<string> _streams;
        private readonly HashSet<string> _eventTypes;
        private readonly Dictionary<string, string> _streamToEventType;

        public EventByTypeIndexPositionTagger(string[] eventTypes)
        {
            if (eventTypes == null) throw new ArgumentNullException("eventTypes");
            if (eventTypes.Length == 0) throw new ArgumentException("eventTypes");
            _eventTypes = new HashSet<string>(eventTypes);
            _streams = new HashSet<string>(from eventType in eventTypes select "$et-" + eventType);
            _streamToEventType = eventTypes.ToDictionary(v => "$et-" + v, v => v);
        }

        public override bool IsMessageAfterCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            if (previous.Mode_ != CheckpointTag.Mode.EventTypeIndex)
                throw new ArgumentException("Mode.EventTypeIndex expected", "previous");
            if (committedEvent.Data.Position.CommitPosition <= 0)
                throw new ArgumentException("complete TF position required", "committedEvent");

            return committedEvent.Data.Position > previous.Position;
        }

        public override CheckpointTag MakeCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            var byIndex = _streams.Contains(committedEvent.Data.PositionStreamId);
            //var byEvent = _eventTypes.Contains(committedEvent.Data.EventType);
            //if (!byEvent && !byIndex)
            //    throw new InvalidOperationException(
            //        string.Format(
            //            "Invalid stream and/or event type'{0}'/'{1}'", committedEvent.Data.PositionStreamId,
            //            committedEvent.Data.EventType));

            return byIndex
                       ? previous.UpdateEventTypeIndexPosition(
                           committedEvent.Data.Position, _streamToEventType[committedEvent.Data.PositionStreamId],
                           committedEvent.Data.PositionSequenceNumber)
                       : previous.UpdateEventTypeIndexPosition(committedEvent.Data.Position);
        }

        public override CheckpointTag MakeZeroCheckpointTag()
        {
            return CheckpointTag.FromEventTypeIndexPositions(
                new TFPos(0, -1), _eventTypes.ToDictionary(v => v, v => ExpectedVersion.NoStream));
        }

        public override bool IsCompatible(CheckpointTag checkpointTag)
        {
            //TODO: should Stream be supported here as well if in the set?
            return checkpointTag.Mode_ == CheckpointTag.Mode.EventTypeIndex
                   && checkpointTag.Streams.All(v => _eventTypes.Contains(v.Key));
        }
    }
}
