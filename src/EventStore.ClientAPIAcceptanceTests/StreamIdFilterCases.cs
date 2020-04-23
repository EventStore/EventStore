using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EventStore.ClientAPI.Tests {
	public class StreamIdFilterCases : IEnumerable<object[]> {
		public IEnumerator<object[]> GetEnumerator() {
			var useSslCases = new[] {true, false};

			foreach (var useSsl in useSslCases) {
				yield return new object[] {useSsl, StreamIdPrefix, nameof(StreamIdPrefix)};
				yield return new object[] {useSsl, StreamIdRegex, nameof(StreamIdRegex)};
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private static readonly Func<string, Filter> StreamIdPrefix = prefix => Filter.StreamId.Prefix(prefix);

		private static readonly Func<string, Filter> StreamIdRegex = prefix =>
			Filter.StreamId.Regex(new Regex($"^{prefix}"));

		public enum FilterType {
			Prefix, 
			Regex
		}
		public class StreamIdFilterCase {
			public StreamIdFilterCase(bool useSsl, FilterType filterType) {
				UseSsl = useSsl;
				FilterType = filterType;
			}

			public bool UseSsl { get; }
			public FilterType FilterType { get; }

			public Filter CreateFilter(string prefix) => FilterType switch
			{
				FilterType.Prefix => StreamIdPrefix(prefix),
				FilterType.Regex => StreamIdRegex(prefix),
				_ => throw new NotImplementedException(),
			};
		}
	}
}
