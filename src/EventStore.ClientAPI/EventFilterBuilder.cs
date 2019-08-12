using System.Collections.Generic;
using System.Text.RegularExpressions;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI {
	public class EventFilterBuilder {
		private readonly List<string> _eventFilters;
		private readonly List<string> _streamFilters;

		internal EventFilterBuilder() {
			_eventFilters = new List<string>();
			_streamFilters = new List<string>();
		}

		public EventFilterBuilder WithEventPrefixFilter(string eventPrefixFilter) {
			Ensure.NotNull(eventPrefixFilter.GetType(), nameof(eventPrefixFilter));
			_eventFilters.Add(eventPrefixFilter);
			return this;
		}

		public EventFilterBuilder WithEventFilter(Regex eventFilter) {
			Ensure.NotNull(eventFilter, nameof(eventFilter));
			_eventFilters.Add(eventFilter.ToString());
			return this;
		}

		public EventFilterBuilder WithStreamPrefixFilter(string streamPrefixFilter) {
			Ensure.NotNull(streamPrefixFilter, nameof(streamPrefixFilter));
			_streamFilters.Add(streamPrefixFilter);
			return this;
		}
		
		public EventFilterBuilder WithStreamFilter(Regex streamFilter) {
			Ensure.NotNull(streamFilter, nameof(streamFilter));
			_streamFilters.Add(streamFilter.ToString());
			return this;
		}

		public EventFilterBuilder ExcludeSystemEvents() {
			_eventFilters.Add(@"^[^\$].*");
			return this;
		}

		public EventFilter Build() {
			return new EventFilter(_eventFilters.ToArray(), _streamFilters.ToArray());
		}
	}

	public sealed class EventFilter {
		public readonly string[] StreamFilters;
		public readonly string[] EventFilters;

		internal EventFilter(string[] eventFilters, string[] streamFilters) {
			EventFilters = eventFilters;
			StreamFilters = streamFilters;
		}

		public static EventFilterBuilder Create() {
			return new EventFilterBuilder();
		}
	}
}
