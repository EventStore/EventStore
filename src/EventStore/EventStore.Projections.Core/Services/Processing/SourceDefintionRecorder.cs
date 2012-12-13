using System.Collections.Generic;

namespace EventStore.Projections.Core.Services.Processing
{
    public class SourceDefintionRecorder : QuerySourceProcessingStrategyBuilder
    {
        public ProjectionSourceDefintion Build()
        {
            return new ProjectionSourceDefintion
                {
                    AllEvents = _allEvents,
                    AllStreams = _allStreams,
                    ByStream = _byStream,
                    ByCustomPartitions = _byCustomPartitions,
                    Categories = (_categories ?? new List<string>()).ToArray(),
                    Events = (_events ?? new List<string>()).ToArray(),
                    Streams = (_streams ?? new List<string>()).ToArray(),
                    Options = _options,
                };
        }
    }
}