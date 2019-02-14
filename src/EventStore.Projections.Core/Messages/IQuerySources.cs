namespace EventStore.Projections.Core.Messages {
	public interface IQuerySources {
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

	public static class QuerySourcesExtensions {
		public static bool HasStreams(this IQuerySources sources) {
			var streams = sources.Streams;
			return streams != null && streams.Length > 0;
		}

		public static bool HasCategories(this IQuerySources sources) {
			var categories = sources.Categories;
			return categories != null && categories.Length > 0;
		}

		public static bool HasEvents(this IQuerySources sources) {
			var events = sources.Events;
			return events != null && events.Length > 0;
		}
	}
}
