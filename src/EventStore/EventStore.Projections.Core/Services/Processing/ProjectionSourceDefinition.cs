using System.Linq;
using System.Runtime.Serialization;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    [DataContract]
    public class ProjectionSourceDefinition : IQuerySources, IQueryDefinition
    {
        [DataMember]
        public bool AllEvents { get; set; }

        [DataMember]
        public bool AllStreams { get; set; }

        [DataMember]
        public bool ByStream { get; set; }

        [DataMember]
        public bool ByCustomPartitions { get; set; }

        [DataMember]
        public string[] Categories { get; set; }

        [DataMember]
        public string[] Events { get; set; }

        [DataMember]
        public string[] Streams { get; set; }

        [DataMember]
        public string CatalogStream { get; set; }

        [DataMember]
        public long? LimitingCommitPosition { get; set; }

        [DataMember]
        public QuerySourceOptions Options { get; set; }

        bool IQuerySources.DefinesStateTransform
        {
            get { return Options != null && Options.DefinesStateTransform; }
        }

        bool IQuerySources.DefinesCatalogTransform
        {
            get { return Options != null && Options.DefinesCatalogTransform; }
        }

        bool IQuerySources.ProducesResults
        {
            get { return Options != null && Options.ProducesResults; }
        }

        bool IQuerySources.DefinesFold
        {
            get { return Options != null && Options.DefinesFold; }
        }

        bool IQuerySources.IncludeLinksOption
        {
            get { return Options != null && Options.IncludeLinks; }
        }

        string IQuerySources.ResultStreamNameOption
        {
            get { return Options != null ? Options.ResultStreamName : null; }
        }

        string IQuerySources.PartitionResultStreamNamePatternOption
        {
            get { return Options != null ? Options.PartitionResultStreamNamePattern : null; }
        }

        string IQuerySources.ForceProjectionNameOption
        {
            get { return Options != null ? Options.ForceProjectionName : null; }
        }

        bool IQuerySources.ReorderEventsOption
        {
            get
            {
                return Options != null && Options.ReorderEvents;
            }
        }

        int? IQuerySources.ProcessingLagOption
        {
            get { return Options != null ? Options.ProcessingLag : (int?) null; }
        }

        bool IQuerySources.IsBiState 
        {
            get { return Options != null ? Options.IsBiState : false; }
        }

        [DataMember]
        public string ResultStreamName { get; set; }

        [DataMember]
        public string PartitionResultStreamNamePattern { get; set; }

        [DataMember]
        public string PartitionCatalogStream { get; set; }

        [DataMember]
        public string PartitionResultCatalogStream { get; set; }

        bool IQuerySources.ByStreams
        {
            get { return ByStream; }
        }

        public static ProjectionSourceDefinition From(
            string name, IQuerySources sources, string handlerType, string query)
        {
            var namingBuilder = new ProjectionNamesBuilder(name, sources);
            return new ProjectionSourceDefinition
            {
                AllEvents = sources.AllEvents,
                AllStreams = sources.AllStreams,
                ByStream = sources.ByStreams,
                ByCustomPartitions = sources.ByCustomPartitions,
                Categories = (sources.Categories ?? new string[0]).ToArray(),
                Events = (sources.Events ?? new string[0]).ToArray(),
                Streams = (sources.Streams ?? new string[0]).ToArray(),
                CatalogStream = sources.CatalogStream,
                LimitingCommitPosition = sources.LimitingCommitPosition,
                Options =
                    new QuerySourceOptions
                    {
                        DefinesStateTransform = sources.DefinesStateTransform,
                        DefinesCatalogTransform = sources.DefinesCatalogTransform,
                        ProducesResults = sources.ProducesResults,
                        DefinesFold = sources.DefinesFold,
                        ForceProjectionName = sources.ForceProjectionNameOption,
                        IncludeLinks = sources.IncludeLinksOption,
                        PartitionResultStreamNamePattern = sources.PartitionResultStreamNamePatternOption,
                        ProcessingLag = sources.ProcessingLagOption.GetValueOrDefault(),
                        IsBiState = sources.IsBiState,
                        ReorderEvents = sources.ReorderEventsOption,
                        ResultStreamName = sources.ResultStreamNameOption,
                    },
                ResultStreamName = namingBuilder.GetResultStreamName(),
                PartitionResultStreamNamePattern = namingBuilder.GetPartitionResultStreamNamePattern(),
                PartitionResultCatalogStream = namingBuilder.GetPartitionResultCatalogStreamName(),
                PartitionCatalogStream = namingBuilder.GetPartitionCatalogStreamName(),
                HandlerType = handlerType,
                Query = query
            };
        }

        public string HandlerType { get; private set; }

        public string Query { get; private set; }
    }
}