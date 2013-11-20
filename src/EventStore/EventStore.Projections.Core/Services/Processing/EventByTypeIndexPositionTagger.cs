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

        public EventByTypeIndexPositionTagger(int phase, string[] eventTypes): base(phase)
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
            if (previous.Phase < Phase)
                return true;
            if (previous.Mode_ != CheckpointTag.Mode.EventTypeIndex)
                throw new ArgumentException("Mode.EventTypeIndex expected", "previous");
            if (committedEvent.Data.OriginalPosition.CommitPosition <= 0)
                throw new ArgumentException("complete TF position required", "committedEvent");

            return committedEvent.Data.OriginalPosition > previous.Position;
        }

        public override CheckpointTag MakeCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            if (previous.Phase != Phase)
                throw new ArgumentException(
                    string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, previous.Phase));

            if (committedEvent.Data.OriginalPosition < previous.Position)
                throw new InvalidOperationException(
                    string.Format(
                        "Cannot make a checkpoint tag at earlier position. '{0}' < '{1}'",
                        committedEvent.Data.OriginalPosition, previous.Position));
            var byIndex = _streams.Contains(committedEvent.Data.PositionStreamId);
            return byIndex
                       ? previous.UpdateEventTypeIndexPosition(
                           committedEvent.Data.OriginalPosition,
                           _streamToEventType[committedEvent.Data.PositionStreamId],
                           committedEvent.Data.PositionSequenceNumber)
                       : previous.UpdateEventTypeIndexPosition(committedEvent.Data.OriginalPosition);
        }

        public override CheckpointTag MakeCheckpointTag(CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionEof partitionEof)
        {
            throw new NotImplementedException();
        }

        public override CheckpointTag MakeZeroCheckpointTag()
        {
            return CheckpointTag.FromEventTypeIndexPositions(
                Phase, new TFPos(0, -1), _eventTypes.ToDictionary(v => v, v => ExpectedVersion.NoStream));
        }

        public override bool IsCompatible(CheckpointTag checkpointTag)
        {
            //TODO: should Stream be supported here as well if in the set?
            return checkpointTag.Mode_ == CheckpointTag.Mode.EventTypeIndex
                   && checkpointTag.Streams.All(v => _eventTypes.Contains(v.Key));
        }

        public override CheckpointTag AdjustTag(CheckpointTag tag)
        {
            if (tag.Phase != Phase)
                throw new ArgumentException(
                    string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, tag.Phase), "tag");

            if (tag.Mode_ == CheckpointTag.Mode.EventTypeIndex)
            {
                int p;
                return CheckpointTag.FromEventTypeIndexPositions(
                    tag.Phase, tag.Position,
                    _eventTypes.ToDictionary(v => v, v => tag.Streams.TryGetValue(v, out p) ? p : -1));
            }

            switch (tag.Mode_)
            {
                case CheckpointTag.Mode.MultiStream:
                    throw new NotSupportedException(
                        "Conversion from MultiStream to EventTypeIndex position tag is not supported");
                case CheckpointTag.Mode.Stream:
                    throw new NotSupportedException(
                        "Conversion from Stream to EventTypeIndex position tag is not supported");
                case CheckpointTag.Mode.PreparePosition:
                    throw new NotSupportedException(
                        "Conversion from PreparePosition to EventTypeIndex position tag is not supported");
                case CheckpointTag.Mode.Position:
                    return CheckpointTag.FromEventTypeIndexPositions(
                        tag.Phase, tag.Position, _eventTypes.ToDictionary(v => v, v => -1));
                default:
                    throw new Exception();
            }
        }
    }
}
