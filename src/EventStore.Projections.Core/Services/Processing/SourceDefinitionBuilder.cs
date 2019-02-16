using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public sealed class SourceDefinitionBuilder : IQuerySources // name it!!
	{
		private readonly QuerySourceOptions _options = new QuerySourceOptions();
		private bool _allStreams;
		private List<string> _categories;
		private List<string> _streams;
		private string _catalogStream;
		private bool _allEvents;
		private List<string> _events;
		private bool _byStream;
		private bool _byCustomPartitions;
		private long? _limitingCommitPosotion;

		public SourceDefinitionBuilder() {
			_options.DefinesFold = true;
		}

		public void FromAll() {
			_allStreams = true;
		}

		public void FromCategory(string categoryName) {
			if (_categories == null)
				_categories = new List<string>();
			_categories.Add(categoryName);
		}

		public void FromStream(string streamName) {
			if (_streams == null)
				_streams = new List<string>();
			_streams.Add(streamName);
		}

		public void FromCatalogStream(string catalogStream) {
			_catalogStream = catalogStream;
		}

		public void AllEvents() {
			_allEvents = true;
		}

		public void SetIncludeLinks(bool includeLinks = true) {
			_options.IncludeLinks = includeLinks;
		}

		public void SetDisableParallelism(bool disableParallelism = true) {
			_options.DisableParallelism = disableParallelism;
		}

		public void IncludeEvent(string eventName) {
			if (_events == null)
				_events = new List<string>();
			_events.Add(eventName);
		}

		public void SetByStream() {
			_byStream = true;
		}

		public void SetByCustomPartitions() {
			_byCustomPartitions = true;
		}

		public void SetDefinesStateTransform() {
			_options.DefinesStateTransform = true;
			_options.ProducesResults = true;
		}

		public void SetOutputState() {
			_options.ProducesResults = true;
		}

		public void NoWhen() {
			_options.DefinesFold = false;
		}

		public void SetResultStreamNameOption(string resultStreamName) {
			_options.ResultStreamName = String.IsNullOrWhiteSpace(resultStreamName) ? null : resultStreamName;
		}

		public void SetPartitionResultStreamNamePatternOption(string partitionResultStreamNamePattern) {
			_options.PartitionResultStreamNamePattern = String.IsNullOrWhiteSpace(partitionResultStreamNamePattern)
				? null
				: partitionResultStreamNamePattern;
		}

		public void SetReorderEvents(bool reorderEvents) {
			_options.ReorderEvents = reorderEvents;
		}

		public void SetProcessingLag(int processingLag) {
			_options.ProcessingLag = processingLag;
		}

		public void SetIsBiState(bool isBiState) {
			_options.IsBiState = isBiState;
		}

		public void SetHandlesStreamDeletedNotifications(bool value = true) {
			_options.HandlesDeletedNotifications = value;
		}

		public bool AllStreams {
			get { return _allStreams; }
		}

		public string[] Categories {
			get { return _categories != null ? _categories.ToArray() : null; }
		}

		public string[] Streams {
			get { return _streams != null ? _streams.ToArray() : null; }
		}

		public string CatalogStream {
			get { return _catalogStream; }
		}

		bool IQuerySources.AllEvents {
			get { return _allEvents; }
		}

		public string[] Events {
			get { return _events != null ? _events.ToArray() : null; }
		}

		public bool ByStreams {
			get { return _byStream; }
		}

		public bool ByCustomPartitions {
			get { return _byCustomPartitions; }
		}

		public long? LimitingCommitPosition {
			get { return _limitingCommitPosotion; }
		}

		public bool DefinesStateTransform {
			get { return _options.DefinesStateTransform; }
		}

		public bool DefinesCatalogTransform {
			get { return _options.DefinesCatalogTransform; }
		}

		public bool ProducesResults {
			get { return _options.ProducesResults; }
		}

		public bool DefinesFold {
			get { return _options.DefinesFold; }
		}

		public bool HandlesDeletedNotifications {
			get { return _options.HandlesDeletedNotifications; }
		}

		public bool IncludeLinksOption {
			get { return _options.IncludeLinks; }
		}

		public bool DisableParallelismOption {
			get { return _options.DisableParallelism; }
		}

		public string ResultStreamNameOption {
			get { return _options.ResultStreamName; }
		}

		public string PartitionResultStreamNamePatternOption {
			get { return _options.PartitionResultStreamNamePattern; }
		}

		public bool ReorderEventsOption {
			get { return _options.ReorderEvents; }
		}

		public int? ProcessingLagOption {
			get { return _options.ProcessingLag; }
		}

		public bool IsBiState {
			get { return _options.IsBiState; }
		}

		public static IQuerySources From(Action<SourceDefinitionBuilder> configure) {
			var b = new SourceDefinitionBuilder();
			configure(b);
			return b.Build();
		}

		public IQuerySources Build() {
			return QuerySourcesDefinition.From(this);
		}

		public void SetLimitingCommitPosition(long limitingCommitPosition) {
			_limitingCommitPosotion = limitingCommitPosition;
		}
	}

	[DataContract]
	public class QuerySourceOptions {
		[DataMember] public string ResultStreamName { get; set; }

		[DataMember] public string PartitionResultStreamNamePattern { get; set; }

		[DataMember] public bool ReorderEvents { get; set; }

		[DataMember] public int ProcessingLag { get; set; }

		[DataMember] public bool IsBiState { get; set; }

		[DataMember] public bool DefinesStateTransform { get; set; }

		[DataMember] public bool DefinesCatalogTransform { get; set; }

		[DataMember] public bool ProducesResults { get; set; }

		[DataMember] public bool DefinesFold { get; set; }

		[DataMember] public bool HandlesDeletedNotifications { get; set; }

		[DataMember] public bool IncludeLinks { get; set; }

		[DataMember] public bool DisableParallelism { get; set; }

		protected bool Equals(QuerySourceOptions other) {
			return string.Equals(ResultStreamName, other.ResultStreamName)
			       && string.Equals(PartitionResultStreamNamePattern, other.PartitionResultStreamNamePattern)
			       && ReorderEvents.Equals(other.ReorderEvents) && ProcessingLag == other.ProcessingLag
			       && IsBiState.Equals(other.IsBiState) && DefinesStateTransform.Equals(other.DefinesStateTransform)
			       && DefinesCatalogTransform.Equals(other.DefinesCatalogTransform)
			       && ProducesResults.Equals(other.ProducesResults) && DefinesFold.Equals(other.DefinesFold)
			       && HandlesDeletedNotifications.Equals(other.HandlesDeletedNotifications)
			       && IncludeLinks.Equals(other.IncludeLinks);
		}

		public override bool Equals(object obj) {
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((QuerySourceOptions)obj);
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = (ResultStreamName != null ? ResultStreamName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (PartitionResultStreamNamePattern != null
					           ? PartitionResultStreamNamePattern.GetHashCode()
					           : 0);
				hashCode = (hashCode * 397) ^ ReorderEvents.GetHashCode();
				hashCode = (hashCode * 397) ^ ProcessingLag;
				hashCode = (hashCode * 397) ^ IsBiState.GetHashCode();
				hashCode = (hashCode * 397) ^ DefinesStateTransform.GetHashCode();
				hashCode = (hashCode * 397) ^ DefinesCatalogTransform.GetHashCode();
				hashCode = (hashCode * 397) ^ ProducesResults.GetHashCode();
				hashCode = (hashCode * 397) ^ DefinesFold.GetHashCode();
				hashCode = (hashCode * 397) ^ HandlesDeletedNotifications.GetHashCode();
				hashCode = (hashCode * 397) ^ IncludeLinks.GetHashCode();
				return hashCode;
			}
		}
	}
}
