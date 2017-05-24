namespace EventStore.Projections.Core.Messages
{
    public interface IQuerySources
    {
        bool AllStreams { get; }

        string[] Categories { get; }

        string[] Streams { get; }

        string CatalogStream { get; }

        bool AllEvents { get; }

        string[] Events { get; }

        bool ByStreams { get; }

        bool ByCustomPartitions { get; }

        bool DefinesStateTransform { get; }

        bool DefinesCatalogTransform { get; }

        bool DefinesFold { get; }

        bool HandlesDeletedNotifications { get; }

        bool ProducesResults { get; }

        bool IsBiState { get; }

        bool IncludeLinksOption { get; }

        bool DisableParallelismOption { get; }

        string ResultStreamNameOption { get; }

        string PartitionResultStreamNamePatternOption { get; }

        bool ReorderEventsOption { get; }

        int? ProcessingLagOption { get; }

        long? LimitingCommitPosition { get; }
    }

    public static class QuerySourcesExtensions
    {
        public static bool HasStreams(this IQuerySources sources) => sources.Streams?.Length > 0;

        public static bool HasCategories(this IQuerySources sources) => sources.Categories?.Length > 0;

        public static bool HasEvents(this IQuerySources sources) => sources.Events?.Length > 0;
    }
}
