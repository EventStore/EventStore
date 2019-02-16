using System.Linq;
using System.Runtime.Serialization;

namespace EventStore.Projections.Core.Messages {
	[DataContract]
	public class QuerySourcesDefinition : IQuerySources {
		[DataMember(Name = "allStreams")] public bool AllStreams { get; set; }

		[DataMember(Name = "categories")] public string[] Categories { get; set; }

		[DataMember(Name = "streams")] public string[] Streams { get; set; }

		[DataMember(Name = "catalogStream")] public string CatalogStream { get; set; }

		[DataMember(Name = "allEvents")] public bool AllEvents { get; set; }

		[DataMember(Name = "events")] public string[] Events { get; set; }

		[DataMember(Name = "byStreams")] public bool ByStreams { get; set; }

		[DataMember(Name = "byCustomPartitions")]
		public bool ByCustomPartitions { get; set; }

		[DataMember(Name = "limitingCommitPosition")]
		public long? LimitingCommitPosition { get; set; }

		bool IQuerySources.DefinesStateTransform {
			get { return Options != null && Options.DefinesStateTransform; }
		}

		bool IQuerySources.DefinesCatalogTransform {
			get { return Options != null && Options.DefinesCatalogTransform; }
		}

		bool IQuerySources.ProducesResults {
			get { return Options != null && Options.ProducesResults; }
		}

		bool IQuerySources.DefinesFold {
			get { return Options != null && Options.DefinesFold; }
		}

		bool IQuerySources.HandlesDeletedNotifications {
			get { return Options != null && Options.HandlesDeletedNotifications; }
		}

		bool IQuerySources.IncludeLinksOption {
			get { return Options != null && Options.IncludeLinks; }
		}

		bool IQuerySources.DisableParallelismOption {
			get { return Options != null && Options.DisableParallelism; }
		}

		string IQuerySources.ResultStreamNameOption {
			get { return Options != null ? Options.ResultStreamName : null; }
		}

		string IQuerySources.PartitionResultStreamNamePatternOption {
			get { return Options != null ? Options.PartitionResultStreamNamePattern : null; }
		}

		bool IQuerySources.ReorderEventsOption {
			get { return Options != null && Options.ReorderEvents; }
		}

		int? IQuerySources.ProcessingLagOption {
			get { return Options != null ? Options.ProcessingLag : null; }
		}

		bool IQuerySources.IsBiState {
			get { return Options != null ? Options.IsBiState : false; }
		}

		[DataMember(Name = "options")] public QuerySourcesDefinitionOptions Options { get; set; }

		public static QuerySourcesDefinition From(IQuerySources sources) {
			return new QuerySourcesDefinition {
				AllEvents = sources.AllEvents,
				AllStreams = sources.AllStreams,
				ByStreams = sources.ByStreams,
				ByCustomPartitions = sources.ByCustomPartitions,
				Categories = (sources.Categories ?? new string[0]).ToArray(),
				Events = (sources.Events ?? new string[0]).ToArray(),
				Streams = (sources.Streams ?? new string[0]).ToArray(),
				CatalogStream = sources.CatalogStream,
				LimitingCommitPosition = sources.LimitingCommitPosition,
				Options =
					new QuerySourcesDefinitionOptions {
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
					}
			};
		}
	}
}
