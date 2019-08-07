using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI {
	public class StreamFilterBuilder {
		private List<string> _eventFilters;

		internal StreamFilterBuilder() {
			_eventFilters = new List<string>();
		}

		public StreamFilterBuilder WithEventFilter(string eventFilter) {
			Ensure.NotNull(eventFilter.GetType(), nameof(eventFilter));
			_eventFilters.Add(eventFilter);
			return this;
		}

		public StreamFilterBuilder WithEventFilter(Regex eventFilter) {
			Ensure.NotNull(eventFilter, nameof(eventFilter));
			_eventFilters.Add(eventFilter.ToString());
			return this;
		}

		public StreamFilterBuilder ExcludeSystemEvents() {
			_eventFilters.Add(@"^[^\$].*");
			return this;
		}

		public StreamFilter Build() {
			return new StreamFilter(_eventFilters.ToArray());
		}
	}

	public sealed class StreamFilter {
		public readonly string[] EventFilters;

		internal StreamFilter(string[] eventFilters) {
			EventFilters = eventFilters;
		}

		public static StreamFilterBuilder Create() {
			return new StreamFilterBuilder();
		}
	}
}
