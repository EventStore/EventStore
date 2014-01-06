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
    public class CatalogStreamPositionTagger : PositionTagger
    {
        private readonly string _catalogStream;

        public CatalogStreamPositionTagger(int phase, string catalogStream)
            : base(phase)
        {
            _catalogStream = catalogStream;
        }

        public override bool IsMessageAfterCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            if (previous.Phase < Phase)
                return true;
            if (previous.Mode_ != CheckpointTag.Mode.ByStream)
                throw new ArgumentException("Mode.Stream expected", "previous");
            return committedEvent.Data.PositionStreamId == _catalogStream
                   && committedEvent.Data.PositionSequenceNumber > previous.CatalogPosition;
        }

        public override CheckpointTag MakeCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            if (previous.Phase != Phase)
                throw new ArgumentException(
                    string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, previous.Phase));

            if (committedEvent.Data.PositionStreamId != _catalogStream)
                throw new InvalidOperationException(
                    string.Format(
                        "Invalid catalog stream '{0}'.  Expected catalog stream is '{1}'",
                        committedEvent.Data.EventStreamId, _catalogStream));

            return CheckpointTag.FromByStreamPosition(
                previous.Phase, "", committedEvent.Data.PositionSequenceNumber, null,
                -1, previous.CommitPosition.GetValueOrDefault());
        }

        public override CheckpointTag MakeCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionEof partitionEof)
        {
            throw new NotImplementedException();
        }

        public override CheckpointTag MakeZeroCheckpointTag()
        {
            return CheckpointTag.FromByStreamPosition(
                Phase, "", -1, null,
                -1, int.MinValue);
        }

        public override bool IsCompatible(CheckpointTag checkpointTag)
        {
            return checkpointTag.Mode_ == CheckpointTag.Mode.ByStream && checkpointTag.CatalogStream == "";
        }

        public override CheckpointTag AdjustTag(CheckpointTag tag)
        {
            if (tag.Phase < Phase)
                return tag;
            if (tag.Phase > Phase)
                throw new ArgumentException(
                    string.Format("Invalid checkpoint tag phase.  Expected less or equal to: {0} Was: {1}", Phase, tag.Phase), "tag");

            if (tag.Mode_ == CheckpointTag.Mode.ByStream)
                return tag;
            switch (tag.Mode_)
            {
                default:
                    throw new NotSupportedException("Conversion is not supported");
            }
        }
    }
}
