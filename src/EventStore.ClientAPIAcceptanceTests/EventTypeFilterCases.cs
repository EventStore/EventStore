using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EventStore.ClientAPI.Tests {
	public class EventTypeFilterCases : IEnumerable<object[]> {
		public IEnumerator<object[]> GetEnumerator() {
			var sslTypesCases = EventStoreClientAPITest.SslTypes;

			foreach (var sslType in sslTypesCases) {
				yield return new object[] {sslType, EventTypePrefix, nameof(EventTypePrefix)};
				yield return new object[] {sslType, EventTypeRegex, nameof(EventTypeRegex)};
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private static readonly Func<string, Filter> EventTypePrefix = prefix => Filter.EventType.Prefix(prefix);

		private static readonly Func<string, Filter> EventTypeRegex = prefix =>
			Filter.EventType.Regex(new Regex($"^{prefix}"));
	}
}
