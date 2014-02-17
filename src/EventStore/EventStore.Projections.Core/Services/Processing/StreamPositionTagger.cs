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
using System.Linq;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class StreamPositionTagger : PositionTagger
    {
        private readonly string _stream;

        public StreamPositionTagger(int phase, string stream): base(phase)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (string.IsNullOrEmpty(stream)) throw new ArgumentException("stream");
            _stream = stream;
        }

        public override bool IsMessageAfterCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            if (previous.Phase < Phase)
                return true;
            if (previous.Mode_ != CheckpointTag.Mode.Stream)
                throw new ArgumentException("Mode.Stream expected", "previous");
            return committedEvent.Data.PositionStreamId == _stream
                   && committedEvent.Data.PositionSequenceNumber > previous.Streams[_stream];
        }

        public override CheckpointTag MakeCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent)
        {
            if (previous.Phase != Phase)
                throw new ArgumentException(
                    string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, previous.Phase));

            if (committedEvent.Data.PositionStreamId != _stream)
                throw new InvalidOperationException(
                    string.Format(
                        "Invalid stream '{0}'.  Expected stream is '{1}'", committedEvent.Data.EventStreamId, _stream));
            return CheckpointTag.FromStreamPosition(previous.Phase, committedEvent.Data.PositionStreamId, committedEvent.Data.PositionSequenceNumber);
        }

        public override CheckpointTag MakeCheckpointTag(CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionEof partitionEof)
        {
            throw new NotImplementedException();
        }

        public override CheckpointTag MakeCheckpointTag(
            CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionDeleted partitionDeleted)
        {
            if (previous.Phase != Phase)
                throw new ArgumentException(
                    string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, previous.Phase));

            if (partitionDeleted.PositionStreamId != _stream)
                throw new InvalidOperationException(
                    string.Format(
                        "Invalid stream '{0}'.  Expected stream is '{1}'", partitionDeleted.Partition, _stream));

            // return ordinary checkpoint tag (suitable for fromCategory.foreachStream as well as for regular fromStream
            return CheckpointTag.FromStreamPosition(
                previous.Phase, partitionDeleted.PositionStreamId, partitionDeleted.PositionEventNumber.Value);
        }

        public override CheckpointTag MakeZeroCheckpointTag()
        {
            return CheckpointTag.FromStreamPosition(Phase, _stream, -1);
        }

        public override bool IsCompatible(CheckpointTag checkpointTag)
        {
            return checkpointTag.Mode_ == CheckpointTag.Mode.Stream && checkpointTag.Streams.Keys.First() == _stream;
        }

        public override CheckpointTag AdjustTag(CheckpointTag tag)
        {
            if (tag.Phase < Phase)
                return tag;
            if (tag.Phase > Phase)
                throw new ArgumentException(
                    string.Format("Invalid checkpoint tag phase.  Expected less or equal to: {0} Was: {1}", Phase, tag.Phase), "tag");


            if (tag.Mode_ == CheckpointTag.Mode.Stream)
            {
                int p;
                return CheckpointTag.FromStreamPosition(
                    tag.Phase, _stream, tag.Streams.TryGetValue(_stream, out p) ? p : -1);
            }
            switch (tag.Mode_)
            {
                case CheckpointTag.Mode.EventTypeIndex:
                    throw new NotSupportedException(
                        "Conversion from EventTypeIndex to Stream position tag is not supported");
                case CheckpointTag.Mode.PreparePosition:
                    throw new NotSupportedException(
                        "Conversion from PreparePosition to Stream position tag is not supported");
                case CheckpointTag.Mode.MultiStream:
                    int p;
                    return CheckpointTag.FromStreamPosition(
                        tag.Phase, _stream, tag.Streams.TryGetValue(_stream, out p) ? p : -1);
                case CheckpointTag.Mode.Position:
                    throw new NotSupportedException("Conversion from Position to Stream position tag is not supported");
                default:
                    throw new Exception();
            }
        }
    }
}
