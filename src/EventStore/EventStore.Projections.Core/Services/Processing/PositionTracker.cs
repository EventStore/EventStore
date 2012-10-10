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
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class PositionTracker
    {
        internal readonly PositionTagger _positionTagger;
        private long _lastEventPreparePosition = -1;
        private CheckpointTag _lastTag = null;

        public PositionTracker(PositionTagger positionTagger)
        {
            _positionTagger = positionTagger;
        }

        //NOTE: to be used for statistics (90% done)
        public long LastEventPrepaprePosition
        {
            get { return _lastEventPreparePosition; }
        }

        public CheckpointTag LastTag
        {
            get { return _lastTag; }
        }

        public void Update(ProjectionMessage.Projections.CommittedEventReceived comittedEvent)
        {
            var newTag = _positionTagger.MakeCheckpointTag(comittedEvent);
            UpdateByCheckpointTagForward(newTag);
            UpdatePosition(comittedEvent.Position);
        }

        public void UpdateByCheckpointTagForward(CheckpointTag newTag)
        {
            if (newTag <= _lastTag)
                throw new InvalidOperationException(
                    string.Format("Event at checkpoint tag {0} has been already processed", newTag));
            _lastTag = newTag;
        }

        public void UpdatePosition(EventPosition position)
        {
            if (position.PreparePosition <= _lastEventPreparePosition) // handle prepare only
                throw new InvalidOperationException(
                    string.Format("Event at position {0} has been already processed", position));
            _lastEventPreparePosition = position.PreparePosition;
        }

        public void UpdateByCheckpointTag(CheckpointTag checkpointTag)
        {
            if (_lastEventPreparePosition != -1 || _lastTag != null)
                throw new InvalidOperationException("Posistion tagger has be already updated");
            _lastEventPreparePosition = checkpointTag.Position.PreparePosition;
            _lastTag = checkpointTag;
        }
    }
}