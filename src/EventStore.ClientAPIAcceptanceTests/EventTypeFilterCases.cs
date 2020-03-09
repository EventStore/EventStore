using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EventStore.ClientAPI.Tests {
	public class EventTypeFilterCases : IEnumerable<object[]> {
		public IEnumerator<object[]> GetEnumerator() {
			var useSslCases = new[] { true, false };

			foreach (var useSsl in useSslCases) {
				yield return new object[] { new Case(useSsl, FilterType.Prefix) };
				yield return new object[] { new Case(useSsl, FilterType.Regex) };
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private static readonly Func<string, Filter> EventTypePrefix = prefix => Filter.EventType.Prefix(prefix);

		private static readonly Func<string, Filter> EventTypeRegex = prefix =>
			Filter.EventType.Regex(new Regex($"^{prefix}"));

		public enum FilterType {
			Prefix,
			Regex
		}

		public class Case {
			public Case(bool useSsl, FilterType filterType) {
				UseSsl = useSsl;
				FilterType = filterType;
			}

			public bool UseSsl { get; }
			public FilterType FilterType { get; }
			public Filter CreateFilter(string filter) => FilterType switch
			{
				FilterType.Prefix => EventTypePrefix(filter),
				FilterType.Regex => EventTypeRegex(filter),
				_ => throw new NotImplementedException()
			};
		}
	}
}
