using System.Linq;
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
        public string CatalogStream { get; set; }

        [DataMember]
        public long? LimitingCommitPosition { get; set; }

        [DataMember]
        public QuerySourceOptions Options { get; set; }

        bool IQuerySources.DefinesStateTransform => Options?.DefinesStateTransform == true;

        bool IQuerySources.DefinesCatalogTransform => Options?.DefinesCatalogTransform == true;

        bool IQuerySources.ProducesResults => Options?.ProducesResults == true;

        bool IQuerySources.DefinesFold => Options?.DefinesFold == true;

        bool IQuerySources.HandlesDeletedNotifications => Options?.HandlesDeletedNotifications == true;

        bool IQuerySources.IncludeLinksOption => Options?.IncludeLinks == true;

        bool IQuerySources.DisableParallelismOption => Options?.DisableParallelism == true;

        string IQuerySources.ResultStreamNameOption => Options?.ResultStreamName;

        string IQuerySources.PartitionResultStreamNamePatternOption => Options?.PartitionResultStreamNamePattern;

        bool IQuerySources.ReorderEventsOption => Options?.ReorderEvents == true;

        int? IQuerySources.ProcessingLagOption => Options?.ProcessingLag;

        bool IQuerySources.IsBiState => Options?.IsBiState == true;

        bool IQuerySources.ByStreams => ByStream;

        public static ProjectionSourceDefinition From(IQuerySources sources)
        {
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
                        HandlesDeletedNotifications = sources.HandlesDeletedNotifications,
                        IncludeLinks = sources.IncludeLinksOption,
                        DisableParallelism = sources.DisableParallelismOption,
                        PartitionResultStreamNamePattern = sources.PartitionResultStreamNamePatternOption,
                        ProcessingLag = sources.ProcessingLagOption.GetValueOrDefault(),
                        IsBiState = sources.IsBiState,
                        ReorderEvents = sources.ReorderEventsOption,
                        ResultStreamName = sources.ResultStreamNameOption,
                    },
            };
        }

        private bool Equals(string[] a, string[] b)
        {
            bool aEmpty = (a == null || a.Length == 0);
            bool bEmpty = (b == null || b.Length == 0);
            if (aEmpty && bEmpty)
                return true;
            if (aEmpty || bEmpty)
                return false;
            return a.SequenceEqual(b);

        }

        protected bool Equals(ProjectionSourceDefinition other)
        {
            return AllEvents.Equals(other.AllEvents) && AllStreams.Equals(other.AllStreams)
                   && ByStream.Equals(other.ByStream) && ByCustomPartitions.Equals(other.ByCustomPartitions)
                   && Equals(Categories, other.Categories) && Equals(Events, other.Events)
                   && Equals(Streams, other.Streams) && string.Equals(CatalogStream, other.CatalogStream)
                   && LimitingCommitPosition == other.LimitingCommitPosition && Equals(Options, other.Options);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ProjectionSourceDefinition) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = AllEvents.GetHashCode();
                hashCode = (hashCode*397) ^ AllStreams.GetHashCode();
                hashCode = (hashCode*397) ^ ByStream.GetHashCode();
                hashCode = (hashCode*397) ^ ByCustomPartitions.GetHashCode();
                hashCode = (hashCode*397) ^ (CatalogStream?.GetHashCode() ?? 0);
                hashCode = (hashCode*397) ^ LimitingCommitPosition.GetHashCode();
                hashCode = (hashCode*397) ^ (Options?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}