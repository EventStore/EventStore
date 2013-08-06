using System;
using System.Collections.Generic;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Standard;

namespace EventStore.Projections.Core.Services.Processing
{
    public class SourceDefinitionRecorder : QuerySourceProcessingStrategyBuilder
    {
        public ProjectionSourceDefinition Build(string name)
        {
            var namingBuilder = new ProjectionNamesBuilder(name, _options);
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

        public QuerySourcesDefinition Build()
        {
            return new QuerySourcesDefinition
            {
                AllEvents = _allEvents,
                AllStreams = _allStreams,
                ByStreams = _byStream,
                ByCustomPartitions = _byCustomPartitions,
                Categories = (_categories ?? new List<string>()).ToArray(),
                Events = (_events ?? new List<string>()).ToArray(),
                Streams = (_streams ?? new List<string>()).ToArray(),
                DefinesStateTransform = _definesStateTransform,
                Options =
                    new QuerySourcesDefinitionOptions
                    {
                        ForceProjectionName = _options.ForceProjectionName,
                        IncludeLinks = _options.IncludeLinks,
                        PartitionResultStreamNamePattern = _options.PartitionResultStreamNamePattern,
                        ProcessingLag = _options.ProcessingLag,
                        ReorderEvents = _options.ReorderEvents,
                        ResultStreamName = _options.ResultStreamName
                    }
            };
        }

        public static IQuerySources From(Action<QuerySourceProcessingStrategyBuilder> configure)
        {
            var b = new SourceDefinitionRecorder();
            configure(b);
            return b.Build();
        }
    }
}