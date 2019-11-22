using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EventStore.ClientAPI.Tests {
	public class EventTypeFilterCases : IEnumerable<object[]> {
		public IEnumerator<object[]> GetEnumerator() {
			var useSslCases = new[] {true, false};

			foreach (var useSsl in useSslCases) {
				yield return new object[] {useSsl, EventTypePrefix, nameof(EventTypePrefix)};
				yield return new object[] {useSsl, EventTypeRegex, nameof(EventTypeRegex)};
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private static readonly Func<string, Filter> EventTypePrefix = prefix => Filter.EventType.Prefix(prefix);

		private static readonly Func<string, Filter> EventTypeRegex = prefix =>
			Filter.EventType.Regex(new Regex($"^{prefix}"));
	}
}
