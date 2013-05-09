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
    public class MultiStreamPositionTagger : PositionTagger
    {
        private readonly HashSet<string> _streams;

        public MultiStreamPositionTagger(string[] streams)
        {
            if (streams == null) throw new ArgumentNullException("streams");
            if (streams.Length == 0) throw new ArgumentException("streams");
            _streams = new HashSet<string>(streams);
        }

        public override bool IsMessageAfterCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            if (previous.Mode_ != CheckpointTag.Mode.MultiStream)
                throw new ArgumentException("Mode.MultiStream expected", "previous");
            return _streams.Contains(committedEvent.Data.PositionStreamId)
                   && committedEvent.Data.PositionSequenceNumber > previous.Streams[committedEvent.Data.PositionStreamId];
        }

        public override CheckpointTag MakeCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            if (!_streams.Contains(committedEvent.Data.PositionStreamId))
                throw new InvalidOperationException(
                    string.Format("Invalid stream '{0}'", committedEvent.Data.EventStreamId));
            return previous.UpdateStreamPosition(
                committedEvent.Data.PositionStreamId, committedEvent.Data.PositionSequenceNumber);
        }

        public override CheckpointTag MakeZeroCheckpointTag()
        {
            return CheckpointTag.FromStreamPositions(_streams.ToDictionary(v => v, v => ExpectedVersion.NoStream));
        }

        public override bool IsCompatible(CheckpointTag checkpointTag)
        {
            //TODO: should Stream be supported here as well if in the set?
            return checkpointTag.Mode_ == CheckpointTag.Mode.MultiStream
                   && checkpointTag.Streams.All(v => _streams.Contains(v.Key));
        }

        public override CheckpointTag AdjustTag(CheckpointTag tag)
        {
            if (tag.Mode_ == CheckpointTag.Mode.MultiStream)
                return tag; // incompatible streams can be safely ignored

            switch (tag.Mode_)
            {
                case CheckpointTag.Mode.EventTypeIndex:
                    throw new NotSupportedException("Conversion from EventTypeIndex to MultiStream position tag is not supported");
                case CheckpointTag.Mode.Stream:
                    int p;
                    return
                        CheckpointTag.FromStreamPositions(
                            _streams.ToDictionary(v => v, v => tag.Streams.TryGetValue(v, out p) ? p : -1));
                case CheckpointTag.Mode.PreparePosition:
                    throw new NotSupportedException("Conversion from PreparePosition to MultiStream position tag is not supported");
                case CheckpointTag.Mode.Position:
                    throw new NotSupportedException("Conversion from Position to MultiStream position tag is not supported");
                default:
                    throw new Exception();
            }
        }
    }
}
