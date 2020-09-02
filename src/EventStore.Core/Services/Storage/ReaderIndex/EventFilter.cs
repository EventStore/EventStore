using System.Linq;
using System.Text.RegularExpressions;
using EventStore.Core.TransactionLogV2.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public static class EventFilter {
		public static IEventFilter None => new AlwaysAllowStrategy();

		public static class StreamName {
			public static IEventFilter Prefixes(params string[] prefixes)
				=> new StreamIdPrefixStrategy(prefixes);

			public static IEventFilter Regex(string regex)
				=> new StreamIdRegexStrategy(regex);
		}

		public static class EventType {
			public static IEventFilter Prefixes(params string[] prefixes)
				=> new EventTypePrefixStrategy(prefixes);

			public static IEventFilter Regex(string regex)
				=> new EventTypeRegexStrategy(regex);
		}

		public class AlwaysAllowStrategy : IEventFilter {
			public bool IsEventAllowed(EventRecord eventRecord) {
				return true;
			}

			public override string ToString() => nameof(AlwaysAllowStrategy);
		}

		private class StreamIdPrefixStrategy : IEventFilter {
			private readonly string[] _expectedPrefixes;

			public StreamIdPrefixStrategy(string[] expectedPrefixes) =>
				_expectedPrefixes = expectedPrefixes;

			public bool IsEventAllowed(EventRecord eventRecord) =>
				_expectedPrefixes.Any(expectedPrefix => eventRecord.EventStreamId.StartsWith(expectedPrefix));

			public override string ToString() =>
				$"{nameof(StreamIdPrefixStrategy)}: ({string.Join(", ", _expectedPrefixes)})";
		}

		private class EventTypePrefixStrategy : IEventFilter {
			private readonly string[] _expectedPrefixes;

			public EventTypePrefixStrategy(string[] expectedPrefixes) =>
				_expectedPrefixes = expectedPrefixes;

			public bool IsEventAllowed(EventRecord eventRecord) =>
				_expectedPrefixes.Any(expectedPrefix => eventRecord.EventType.StartsWith(expectedPrefix));

			public override string ToString() =>
				$"{nameof(EventTypePrefixStrategy)}: ({string.Join(", ", _expectedPrefixes)})";
		}

		private class EventTypeRegexStrategy : IEventFilter {
			private readonly Regex _expectedRegex;

			public EventTypeRegexStrategy(string expectedRegex) =>
				_expectedRegex = new Regex(expectedRegex, RegexOptions.Compiled);

			public bool IsEventAllowed(EventRecord eventRecord) =>
				_expectedRegex.Match(eventRecord.EventType).Success;

			public override string ToString() =>
				$"{nameof(EventTypeRegexStrategy)}: ({string.Join(", ", _expectedRegex)})";
		}

		private class StreamIdRegexStrategy : IEventFilter {
			private readonly Regex _expectedRegex;

			public StreamIdRegexStrategy(string expectedRegex) =>
				_expectedRegex = new Regex(expectedRegex, RegexOptions.Compiled);

			public bool IsEventAllowed(EventRecord eventRecord) =>
				_expectedRegex.Match(eventRecord.EventStreamId).Success;

			public override string ToString() =>
				$"{nameof(StreamIdRegexStrategy)}: ({string.Join(", ", _expectedRegex)})";
		}
	}
}
