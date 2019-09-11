using System;
using System.Linq;
using System.Text.RegularExpressions;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Util {
	public interface IEventFilter {
		bool IsEventAllowed(PrepareLogRecord prepareLogRecord);
	}
	
	public class EventFilter {
	
		public static IEventFilter Get(TcpClientMessageDto.Filter filter) {
			if (filter == null || filter.Data.Length == 0) {
				return new AlwaysAllowStrategy();
			}

			switch (filter.Context) {
				case TcpClientMessageDto.Filter.FilterContext.EventType
					when filter.Type == TcpClientMessageDto.Filter.FilterType.Prefix:
					return new EventTypePrefixStrategy(filter.Data);
				case TcpClientMessageDto.Filter.FilterContext.EventType
					when filter.Type == TcpClientMessageDto.Filter.FilterType.Regex:
					return new EventTypeRegexStrategy(filter.Data[0]);
				case TcpClientMessageDto.Filter.FilterContext.StreamId
					when filter.Type == TcpClientMessageDto.Filter.FilterType.Prefix:
					return new StreamIdPrefixStrategy(filter.Data);
				case TcpClientMessageDto.Filter.FilterContext.StreamId
					when filter.Type == TcpClientMessageDto.Filter.FilterType.Regex:
					return new StreamIdRegexStrategy(filter.Data[0]);
			}
			
			throw new Exception(); // Invalid filter
		}

		private class AlwaysAllowStrategy : IEventFilter {
			public bool IsEventAllowed(PrepareLogRecord prepareLogRecord) {
				return true;
			}
		}

		private class StreamIdPrefixStrategy : IEventFilter {
			private readonly string[] _expectedPrefixes;

			public StreamIdPrefixStrategy(string[] expectedPrefixes) =>
				_expectedPrefixes = expectedPrefixes;

			public bool IsEventAllowed(PrepareLogRecord prepareLogRecord) =>
				_expectedPrefixes.Any(expectedPrefix => prepareLogRecord.EventStreamId.StartsWith(expectedPrefix));
		}

		private class EventTypePrefixStrategy : IEventFilter {
			private readonly string[] _expectedPrefixes;

			public EventTypePrefixStrategy(string[] expectedPrefixes) =>
				_expectedPrefixes = expectedPrefixes;

			public bool IsEventAllowed(PrepareLogRecord prepareLogRecord) =>
				_expectedPrefixes.Any(expectedPrefix => prepareLogRecord.EventType.StartsWith(expectedPrefix));
		}

		private class EventTypeRegexStrategy : IEventFilter {
			private readonly Regex _expectedRegex;

			public EventTypeRegexStrategy(string expectedRegex) =>
				_expectedRegex = new Regex(expectedRegex, RegexOptions.Compiled);

			public bool IsEventAllowed(PrepareLogRecord prepareLogRecord) =>
				_expectedRegex.Match(prepareLogRecord.EventType).Success;
		}

		private class StreamIdRegexStrategy : IEventFilter {
			private readonly Regex _expectedRegex;

			public StreamIdRegexStrategy(string expectedRegex) =>
				_expectedRegex = new Regex(expectedRegex, RegexOptions.Compiled);

			public bool IsEventAllowed(PrepareLogRecord prepareLogRecord) =>
				_expectedRegex.Match(prepareLogRecord.EventStreamId).Success;
		}

		public static (bool Success, string Reason) TryParse(string context, string type, string data, out IEventFilter filter) {
			TcpClientMessageDto.Filter.FilterContext parsedContext;
			switch (context) {
				case "eventtype":
					parsedContext =  TcpClientMessageDto.Filter.FilterContext.EventType;
					break;
				case "streamid":
					parsedContext = TcpClientMessageDto.Filter.FilterContext.StreamId;
					break;
				default:
					filter = null;
					var names = string.Join(", ",Enum.GetNames(typeof(TcpClientMessageDto.Filter.FilterContext)));
					return (false, $"Invalid context please provide one of the following: {names}.");
			}
			
			TcpClientMessageDto.Filter.FilterType parsedType;
			switch (type) {
				case "regex":
					parsedType =  TcpClientMessageDto.Filter.FilterType.Regex;
					break;
				case "prefix":
					parsedType = TcpClientMessageDto.Filter.FilterType.Prefix;
					break;
				default:
					filter = null;
					var names = string.Join(", ",Enum.GetNames(typeof(TcpClientMessageDto.Filter.FilterType)));
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

			filter = Get(new TcpClientMessageDto.Filter(parsedContext, parsedType, data.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries)));
			return (true, null);

		}
	}
}
