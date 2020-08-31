using System;
using System.Linq;
using System.Text.RegularExpressions;
using EventStore.Core.Data;
using EventStore.Core.Messages;
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

		public static IEventFilter Get(TcpClientMessageDto.Filter filter) {
			if (filter == null || filter.Data.Length == 0) {
				return new AlwaysAllowStrategy();
			}

			return filter.Context switch {
				TcpClientMessageDto.Filter.FilterContext.EventType when filter.Type ==
				                                                        TcpClientMessageDto.Filter.FilterType.Prefix =>
				EventType.Prefixes(filter.Data),
				TcpClientMessageDto.Filter.FilterContext.EventType when filter.Type ==
				                                                        TcpClientMessageDto.Filter.FilterType.Regex =>
				EventType.Regex(filter.Data[0]),
				TcpClientMessageDto.Filter.FilterContext.StreamId when filter.Type ==
				                                                       TcpClientMessageDto.Filter.FilterType.Prefix =>
				StreamName.Prefixes(filter.Data),
				TcpClientMessageDto.Filter.FilterContext.StreamId when filter.Type ==
				                                                       TcpClientMessageDto.Filter.FilterType.Regex =>
				StreamName.Regex(filter.Data[0]),
				_ => throw new Exception() // Invalid filter
			};
		}

		private class AlwaysAllowStrategy : IEventFilter {
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

		public static (bool Success, string Reason) TryParse(string context, string type, string data,
			out IEventFilter filter) {
			TcpClientMessageDto.Filter.FilterContext parsedContext;
			switch (context) {
				case "eventtype":
					parsedContext = TcpClientMessageDto.Filter.FilterContext.EventType;
					break;
				case "streamid":
					parsedContext = TcpClientMessageDto.Filter.FilterContext.StreamId;
					break;
				default:
					filter = null;
					var names = string.Join(", ", Enum.GetNames(typeof(TcpClientMessageDto.Filter.FilterContext)));
					return (false, $"Invalid context please provide one of the following: {names}.");
			}

			TcpClientMessageDto.Filter.FilterType parsedType;
			switch (type) {
				case "regex":
					parsedType = TcpClientMessageDto.Filter.FilterType.Regex;
					break;
				case "prefix":
					parsedType = TcpClientMessageDto.Filter.FilterType.Prefix;
					break;
				default:
					filter = null;
					var names = string.Join(", ", Enum.GetNames(typeof(TcpClientMessageDto.Filter.FilterType)));
					return (false, $"Invalid type please provide one of the following: {names}.");
			}

			if (string.IsNullOrEmpty(data)) {
				filter = null;
				return (false, "Please provide a comma delimited list of data with at least one item.");
			}

			if (parsedType == TcpClientMessageDto.Filter.FilterType.Regex) {
				filter = Get(new TcpClientMessageDto.Filter(parsedContext, parsedType, new[] {data}));
				return (true, null);
			}

			filter = Get(new TcpClientMessageDto.Filter(parsedContext, parsedType,
				data.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries)));
			return (true, null);
		}
	}
}
