extern alias GrpcClient;
using System.Text.RegularExpressions;
using GrpcClient::EventStore.Client;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public static class Filter {
	public static readonly IEventFilter ExcludeSystemEvents = EventTypeFilter.ExcludeSystemEvents();

	public static class StreamId {
		public static IEventFilter Prefix(string prefix) {
			return StreamFilter.Prefix(prefix);
		}

		public static IEventFilter Regex(Regex regex) {
			return StreamFilter.RegularExpression(regex);
		}
	}

	public static class EventType {
		public static IEventFilter Prefix(string prefix) {
			return EventTypeFilter.Prefix(prefix);
		}

		public static IEventFilter Regex(Regex regex) {
			return EventTypeFilter.RegularExpression(regex);
		}
	}
}
