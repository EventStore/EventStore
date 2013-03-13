using System.Collections.Generic;

namespace EventStore.Projections.Core.Services.Processing
{
    public class SourceDefinitionRecorder : QuerySourceProcessingStrategyBuilder
    {
        public ProjectionSourceDefinition Build(ProjectionNamesBuilder namingBuilder)
        {
            return new ProjectionSourceDefinition
                {
                    AllEvents = _allEvents,
                    AllStreams = _allStreams,
                    ByStream = _byStream,
                    ByCustomPartitions = _byCustomPartitions,
                    Categories = (_categories ?? new List<string>()).ToArray(),
                    Events = (_events ?? new List<string>()).ToArray(),
                    Streams = (_streams ?? new List<string>()).ToArray(),
                    DefinesStateTransform = _definesStateTransform,
                    Options = _options,
                    ResultStreamName = namingBuilder.GetResultStreamName(),
                    PartitionResultStreamNamePattern = namingBuilder.GetPartitionResultStreamNamePattern(),
                    PartitionResultCatalogStream = namingBuilder.GetPartitionResultCatalogStreamName(),
                    PartitionCatalogStream = namingBuilder.GetPartitionCatalogStreamName(),
                };
        }
    }
}