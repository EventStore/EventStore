using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public static class EventFilter {
		public static IEventFilter DefaultAllFilter => new DefaultAllFilterStrategy();
		public static IEventFilter DefaultStreamFilter => new DefaultStreamFilterStrategy();

		public static class StreamName {
			public static IEventFilter Prefixes(bool isAllStream, params string[] prefixes)
				=> new StreamIdPrefixStrategy(isAllStream, prefixes);

			public static IEventFilter Regex(bool isAllStream, string regex)
				=> new StreamIdRegexStrategy(isAllStream, regex);
		}

		public static class EventType {
			public static IEventFilter Prefixes(bool isAllStream, params string[] prefixes)
				=> new EventTypePrefixStrategy(isAllStream, prefixes);

			public static IEventFilter Regex(bool isAllStream, string regex)
				=> new EventTypeRegexStrategy(isAllStream, regex);
		}

		public static IEventFilter Get(bool isAllStream, TcpClientMessageDto.Filter filter) {
			if (filter == null || filter.Data.Length == 0) {
				return isAllStream ? (IEventFilter)new DefaultAllFilterStrategy() : new DefaultStreamFilterStrategy();
			}

			return filter.Context switch {
				TcpClientMessageDto.Filter.FilterContext.EventType when filter.Type ==
																		TcpClientMessageDto.Filter.FilterType.Prefix =>
				EventType.Prefixes(isAllStream, filter.Data),
				TcpClientMessageDto.Filter.FilterContext.EventType when filter.Type ==
																		TcpClientMessageDto.Filter.FilterType.Regex =>
				EventType.Regex(isAllStream, filter.Data[0]),
				TcpClientMessageDto.Filter.FilterContext.StreamId when filter.Type ==
																	   TcpClientMessageDto.Filter.FilterType.Prefix =>
				StreamName.Prefixes(isAllStream, filter.Data),
				TcpClientMessageDto.Filter.FilterContext.StreamId when filter.Type ==
																	   TcpClientMessageDto.Filter.FilterType.Regex =>
				StreamName.Regex(isAllStream, filter.Data[0]),
				_ => throw new Exception() // Invalid filter
			};
		}

		private sealed class DefaultStreamFilterStrategy : IEventFilter {
			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			public bool IsEventAllowed(EventRecord eventRecord) => true;
		}

		private sealed class DefaultAllFilterStrategy : IEventFilter {
			//first rule that matches from the top is applied
			private (IEventFilter filter, bool allow)[] _allFilters = {
				//immediately allow all non-system events
				(new NonSystemStreamStrategy(), true),
				//disallow persistent subscription to $all checkpoints
				(new OrdinalStreamIdPrefixAndSuffixStrategy("$persistentsubscription-$all::","-checkpoint"), false),
				//disallow persistent subscription to $all parked messages
				(new OrdinalStreamIdPrefixAndSuffixStrategy("$persistentsubscription-$all::","-parked"), false)
			};

			[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
			public bool IsEventAllowed(EventRecord eventRecord) {
				var filters = _allFilters.AsSpan();
				Debug.Assert(filters.Length > 0);
				do {
					var (filter, allow) = filters[0];
					if (filter.IsEventAllowed(eventRecord)) {
						return allow;
					}
					filters = filters[1..];
				} while (!filters.IsEmpty);
				return true;
			}

			public override string ToString() => nameof(DefaultAllFilterStrategy);

			private class NonSystemStreamStrategy : IEventFilter {

				[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
				public bool IsEventAllowed(EventRecord eventRecord) =>
					eventRecord.EventStreamId[0] != '$';

				public override string ToString() => nameof(NonSystemStreamStrategy);
			}

			private class OrdinalStreamIdPrefixAndSuffixStrategy : IEventFilter {
				private readonly string _prefix;
				private readonly string _suffix;
				private readonly int _minLength;

				public OrdinalStreamIdPrefixAndSuffixStrategy(string prefix, string suffix) {
					_prefix = prefix;
					_suffix = suffix;
					_minLength = _prefix.Length + _suffix.Length;
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
				public bool IsEventAllowed(EventRecord eventRecord) =>
					eventRecord.EventStreamId.Length >= _minLength
					&& eventRecord.EventStreamId.StartsWith(_prefix, StringComparison.Ordinal)
					&& eventRecord.EventStreamId.EndsWith(_suffix, StringComparison.Ordinal);
				public override string ToString() =>
					$"{nameof(OrdinalStreamIdPrefixAndSuffixStrategy)}: (prefix: {_prefix}, suffix: {_suffix})";
			}
		}

		private sealed class StreamIdPrefixStrategy : IEventFilter {
			private readonly bool _isAllStream;
			private readonly string[] _expectedPrefixes;

			public StreamIdPrefixStrategy(bool isAllStream, string[] expectedPrefixes) {
				_isAllStream = isAllStream;
				_expectedPrefixes = expectedPrefixes;
			}

			public bool IsEventAllowed(EventRecord eventRecord) =>
				(!_isAllStream || DefaultAllFilter.IsEventAllowed(eventRecord)) &&
				_expectedPrefixes.Any(expectedPrefix => eventRecord.EventStreamId.StartsWith(expectedPrefix));

			public override string ToString() =>
				$"{nameof(StreamIdPrefixStrategy)}: ({string.Join(", ", _expectedPrefixes)})";
		}

		private sealed class EventTypePrefixStrategy : IEventFilter {
			private readonly bool _isAllStream;
			private readonly string[] _expectedPrefixes;

			public EventTypePrefixStrategy(bool isAllStream, string[] expectedPrefixes) {
				_isAllStream = isAllStream;
				_expectedPrefixes = expectedPrefixes;
			}

			public bool IsEventAllowed(EventRecord eventRecord) =>
				(!_isAllStream || DefaultAllFilter.IsEventAllowed(eventRecord)) &&
				_expectedPrefixes.Any(expectedPrefix => eventRecord.EventType.StartsWith(expectedPrefix));

			public override string ToString() =>
				$"{nameof(EventTypePrefixStrategy)}: ({string.Join(", ", _expectedPrefixes)})";
		}

		private sealed class EventTypeRegexStrategy : IEventFilter {
			private readonly bool _isAllStream;
			private readonly Regex _expectedRegex;

			public EventTypeRegexStrategy(bool isAllStream, string expectedRegex) {
				_isAllStream = isAllStream;
				_expectedRegex = new Regex(expectedRegex, RegexOptions.Compiled);
			}

			public bool IsEventAllowed(EventRecord eventRecord) =>
				(!_isAllStream || DefaultAllFilter.IsEventAllowed(eventRecord)) &&
				_expectedRegex.Match(eventRecord.EventType).Success;

			public override string ToString() =>
				$"{nameof(EventTypeRegexStrategy)}: ({string.Join(", ", _expectedRegex)})";
		}

		private sealed class StreamIdRegexStrategy : IEventFilter {
			private readonly bool _isAllStream;
			private readonly Regex _expectedRegex;

			public StreamIdRegexStrategy(bool isAllStream, string expectedRegex) {
				_isAllStream = isAllStream;
				_expectedRegex = new Regex(expectedRegex, RegexOptions.Compiled);
			}

			public bool IsEventAllowed(EventRecord eventRecord) =>
				(!_isAllStream || DefaultAllFilter.IsEventAllowed(eventRecord)) &&
				_expectedRegex.Match(eventRecord.EventStreamId).Success;

			public override string ToString() =>
				$"{nameof(StreamIdRegexStrategy)}: ({string.Join(", ", _expectedRegex)})";
		}

		public static (bool Success, string Reason) TryParse(string context, bool isAllStream, string type, string data,
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
				filter = Get(isAllStream, new TcpClientMessageDto.Filter(parsedContext, parsedType, new[] { data }));
				return (true, null);
			}

			filter = Get(isAllStream, new TcpClientMessageDto.Filter(parsedContext, parsedType,
				data.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)));
			return (true, null);
		}
	}
}
