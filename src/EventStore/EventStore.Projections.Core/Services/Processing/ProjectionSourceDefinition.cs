using System.Runtime.Serialization;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    [DataContract]
    public class ProjectionSourceDefinition : IQuerySources
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
        public QuerySourceProcessingStrategyBuilder.QuerySourceOptions Options { get; set; }

        [DataMember]
        public bool DefinesStateTransform { get; set; }

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

        [DataMember]
        public string ResultStreamName { get; set; }

        [DataMember]
        public string PartitionResultStreamNamePattern { get; set; }

        [DataMember]
        public string PartitionCatalogStream { get; set; }

        [DataMember]
        public string PartitionResultCatalogStream { get; set; }

        bool IQuerySources.ByStreams {
            get { return ByStream; } 
        }
    }
}