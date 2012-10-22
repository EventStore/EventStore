using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using System.Linq;

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

        public override CheckpointTag MakeCheckpointTag(CheckpointTag previous, ProjectionMessage.Projections.CommittedEventDistributed comittedEvent)
        {
            if (!_streams.Contains(comittedEvent.PositionStreamId))
                throw new InvalidOperationException(string.Format("Invalid stream '{0}'", comittedEvent.EventStreamId));
            var resultDictionary = new Dictionary<string, int>();
            foreach (var stream in previous.Streams)
            {
                if (stream.Key == comittedEvent.PositionStreamId)
                {
                    if (comittedEvent.PositionSequenceNumber < stream.Value)
                        throw new InvalidOperationException(string.Format("Cannot make a checkpoint tag before the current position. Stream: '{0}'  Current: {1} Message Position Event SequenceNo: {2}", stream.Key, stream.Value, comittedEvent.PositionSequenceNumber));
                    resultDictionary.Add(stream.Key, comittedEvent.PositionSequenceNumber);
                }
                else
                {
                    resultDictionary.Add(stream.Key, stream.Value);
                }
            }
            if (resultDictionary.Count < previous.Streams.Count)
                resultDictionary.Add(comittedEvent.PositionStreamId, comittedEvent.PositionSequenceNumber);
            return CheckpointTag.FromStreamPositions(resultDictionary, -123);
        }

        public override CheckpointTag MakeZeroCheckpointTag()
        {
            return CheckpointTag.FromStreamPositions(_streams.ToDictionary(v => v, v => ExpectedVersion.NoStream), -1);
        }

        public override bool IsCompatible(CheckpointTag checkpointTag)
        {
            //TODO: should Stream be supported here as well if in the set?
            return checkpointTag.GetMode() == CheckpointTag.Mode.MultiStream
                   && checkpointTag.Streams.All(v => _streams.Contains(v.Key));

        }
    }
}