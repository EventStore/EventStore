using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EventStore.Client.Streams {
	public static class Filters {
		private const string StreamNamePrefix = nameof(StreamNamePrefix);
		private const string StreamNameRegex = nameof(StreamNameRegex);
		private const string EventTypePrefix = nameof(EventTypePrefix);
		private const string EventTypeRegex = nameof(EventTypeRegex);

		private static readonly IDictionary<string, (Func<string, IEventFilter>, Func<string, EventData, EventData>)>
			s_filters =
				new Dictionary<string, (Func<string, IEventFilter>, Func<string, EventData, EventData>)> {
					[StreamNamePrefix] = (StreamFilter.Prefix, (_, e) => e),
					[StreamNameRegex] = (f => StreamFilter.RegularExpression(f), (_, e) => e),
					[EventTypePrefix] = (EventTypeFilter.Prefix,
						(term, e) => new EventData(e.EventId, term, e.Data, e.Metadata, e.ContentType)),
					[EventTypeRegex] = (f => EventTypeFilter.RegularExpression(f),
						(term, e) => new EventData(e.EventId, term, e.Data, e.Metadata, e.ContentType))
				};

		public static readonly IEnumerable<string> All = typeof(Filters)
			.GetFields(BindingFlags.NonPublic | BindingFlags.Static)
			.Where(fi => fi.IsLiteral && !fi.IsInitOnly)
			.Select(fi => (string)fi.GetRawConstantValue());

		public static (Func<string, IEventFilter> getFilter, Func<string, EventData, EventData> prepareEvent)
			GetFilter(string name) => s_filters[name];
	}
}
