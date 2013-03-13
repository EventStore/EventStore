using System.Runtime.Serialization;

namespace EventStore.Projections.Core.Services.Processing
{
    [DataContract]
    public class ProjectionSourceDefinition
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

        [DataMember]
        public string ResultStreamName { get; set; }

        [DataMember]
        public string PartitionResultStreamNamePattern { get; set; }

        [DataMember]
        public string PartitionCatalogStream { get; set; }

        [DataMember]
        public string PartitionResultCatalogStream { get; set; }
    }
}