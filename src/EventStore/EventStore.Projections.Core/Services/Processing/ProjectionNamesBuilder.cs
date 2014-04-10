using System;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionNamesBuilder
    {

        public static class StandardProjections
        {
            public const string StreamsStandardProjection = "$streams";
            public const string StreamByCategoryStandardProjection = "$stream_by_category";
            public const string EventByCategoryStandardProjection = "$by_category";
            public const string EventByTypeStandardProjection = "$by_event_type";
        }

        public const string EventType_ProjectionCheckpoint = "$ProjectionCheckpoint";
        public const string EventType_PartitionCheckpoint = "$Checkpoint";

        public static ProjectionNamesBuilder CreateForTest(string name)
        {
            return new ProjectionNamesBuilder(name);
        }

        private readonly string _name;
        private readonly IQuerySources _sources;
        private readonly string _partitionResultStreamNamePattern;
        private readonly string _resultStreamName;
        private readonly string _partitionCatalogStreamName;
        private readonly string _checkpointStreamName;
        private readonly string _orderStreamName;

        private ProjectionNamesBuilder(string name)
            : this(name, new QuerySourcesDefinition())
        {
        }

        public ProjectionNamesBuilder(string name, IQuerySources sources)
        {
            _name = name;
            _sources = sources;
            _partitionResultStreamNamePattern = _sources.PartitionResultStreamNamePatternOption
                                                ?? ProjectionsStreamPrefix + EffectiveProjectionName + "-{0}"
                                                + ProjectionsStateStreamSuffix;
            _resultStreamName = _sources.ResultStreamNameOption
                                ?? ProjectionsStreamPrefix + EffectiveProjectionName + ProjectionsStateStreamSuffix;
            _partitionCatalogStreamName = ProjectionsStreamPrefix + EffectiveProjectionName
                                          + ProjectionPartitionCatalogStreamSuffix;
            _checkpointStreamName = ProjectionsStreamPrefix + EffectiveProjectionName + ProjectionCheckpointStreamSuffix;
            _orderStreamName = ProjectionsStreamPrefix + EffectiveProjectionName + ProjectionOrderStreamSuffix;
        }

        public string EffectiveProjectionName
        {
            get { return _sources.ForceProjectionNameOption ?? _name; }
        }


        private string GetPartitionResultStreamName(string partitionName)
        {
            return String.Format(GetPartitionResultStreamNamePattern(), partitionName);
        }

        public string GetResultStreamName()
        {
            return _resultStreamName;
        }

        public string GetPartitionResultStreamNamePattern()
        {
            return _sources.PartitionResultStreamNamePatternOption
                   ?? ProjectionsStreamPrefix + EffectiveProjectionName + "-{0}" + ProjectionsStateStreamSuffix;
        }

        private const string ProjectionsStreamPrefix = "$projections-";
        private const string ProjectionsStateStreamSuffix = "-result";
        private const string ProjectionCheckpointStreamSuffix = "-checkpoint";
        private const string ProjectionOrderStreamSuffix = "-order";
        private const string ProjectionPartitionCatalogStreamSuffix = "-partitions";
        private const string CategoryCatalogStreamNamePrefix = "$category-";

        public string GetPartitionCatalogStreamName()
        {
            return _partitionCatalogStreamName;
        }

        public string GetPartitionResultCatalogStreamName()
        {
            return ProjectionsStreamPrefix + EffectiveProjectionName + ProjectionPartitionCatalogStreamSuffix;
        }

        public string MakePartitionResultStreamName(string statePartition)
        {
            return String.IsNullOrEmpty(statePartition)
                ? GetResultStreamName()
                : GetPartitionResultStreamName(statePartition);
        }

        public string MakePartitionCheckpointStreamName(string statePartition)
        {
            if (String.IsNullOrEmpty(statePartition))
                throw new InvalidOperationException("Root partition cannot have a partition checkpoint stream");

            return ProjectionsStreamPrefix + EffectiveProjectionName + "-" + statePartition
                   + ProjectionCheckpointStreamSuffix;
        }


        public string MakeCheckpointStreamName()
        {
            return _checkpointStreamName;
        }

        public string GetOrderStreamName()
        {
            return _orderStreamName;
        }

        public string GetCategoryCatalogStreamName(string category)
        {
            return CategoryCatalogStreamNamePrefix + category;
        }

        public const string _projectionsControlStream = "$projections-$control";
        public const string _projectionsMasterStream = "$projections-$master";
    }
}
