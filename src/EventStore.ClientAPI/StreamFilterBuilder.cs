using System.Collections.Generic;
using System.Text.RegularExpressions;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI {
	public class StreamFilterBuilder {
		private readonly List<string> _eventFilters;
		private readonly List<string> _streamFilters;

		internal StreamFilterBuilder() {
			_eventFilters = new List<string>();
			_streamFilters = new List<string>();
		}

		public StreamFilterBuilder WithEventPrefixFilter(string eventPrefixFilter) {
			Ensure.NotNull(eventPrefixFilter.GetType(), nameof(eventPrefixFilter));
			_eventFilters.Add(eventPrefixFilter);
			return this;
		}

		public StreamFilterBuilder WithEventFilter(Regex eventFilter) {
			Ensure.NotNull(eventFilter, nameof(eventFilter));
			_eventFilters.Add(eventFilter.ToString());
			return this;
		}

		public StreamFilterBuilder WithStreamPrefixFilter(string streamPrefixFilter) {
			Ensure.NotNull(streamPrefixFilter, nameof(streamPrefixFilter));
			_streamFilters.Add(streamPrefixFilter);
			return this;
		}

		public StreamFilterBuilder ExcludeSystemEvents() {
			_eventFilters.Add(@"^[^\$].*");
			return this;
		}

		public StreamFilter Build() {
			return new StreamFilter(_eventFilters.ToArray(), _streamFilters.ToArray());
		}
	}

	public sealed class StreamFilter {
		public readonly string[] StreamFilters;
		public readonly string[] EventFilters;

		internal StreamFilter(string[] eventFilters, string[] streamFilters) {
			EventFilters = eventFilters;
			StreamFilters = streamFilters;
		}

		public static StreamFilterBuilder Create() {
			return new StreamFilterBuilder();
		}
	}
}
