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
    public class TransactionFilePositionTagger : PositionTagger
    {
        public TransactionFilePositionTagger(int phase)
            : base(phase)
        {
        }

        public override bool IsCompatible(CheckpointTag checkpointTag)
        {
            return checkpointTag.Mode_ == CheckpointTag.Mode.Position;
        }

        public override CheckpointTag AdjustTag(CheckpointTag tag)
        {
            if (tag.Phase < Phase)
                return tag;
            if (tag.Phase > Phase)
                throw new ArgumentException(
                    string.Format("Invalid checkpoint tag phase.  Expected less or equal to: {0} Was: {1}", Phase, tag.Phase), "tag");

            if (tag.Mode_ == CheckpointTag.Mode.Position)
                return tag;

            switch (tag.Mode_)
            {
                case CheckpointTag.Mode.EventTypeIndex:
                    return CheckpointTag.FromPosition(
                        tag.Phase, tag.Position.CommitPosition, tag.Position.PreparePosition);
                case CheckpointTag.Mode.Stream:
                    throw new NotSupportedException("Conversion from Stream to Position position tag is not supported");
                case CheckpointTag.Mode.MultiStream:
                    throw new NotSupportedException(
                        "Conversion from MultiStream to Position position tag is not supported");
                case CheckpointTag.Mode.PreparePosition:
                    throw new NotSupportedException(
                        "Conversion from PreparePosition to Position position tag is not supported");
                default:
                    throw new Exception();
            }
        }

        public override bool IsMessageAfterCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            if (previous.Phase < Phase)
                return true;
            if (previous.Mode_ != CheckpointTag.Mode.Position)
                throw new ArgumentException("Mode.Position expected", "previous");
            return committedEvent.Data.Position > previous.Position;
        }

        public override CheckpointTag MakeCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            if (previous.Phase != Phase)
                throw new ArgumentException(
                    string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, previous.Phase));

            return CheckpointTag.FromPosition(previous.Phase, committedEvent.Data.Position);
        }

        public override CheckpointTag MakeCheckpointTag(CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionEof partitionEof)
        {
            throw new NotImplementedException();
        }

        public override CheckpointTag MakeCheckpointTag(CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionDeleted partitionDeleted)
        {
            if (previous.Phase != Phase)
                throw new ArgumentException(
                    string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, previous.Phase));

            if (partitionDeleted.DeleteEventPosition == null)
                throw new ArgumentException("Invalid partiton deleted message. DeleteEventPosition required");

            return CheckpointTag.FromPosition(previous.Phase, partitionDeleted.DeleteEventPosition.Value);
        }

        public override CheckpointTag MakeZeroCheckpointTag()
        {
            return CheckpointTag.FromPosition(Phase, 0, -1);
        }
    }
}
