extern alias GrpcClient;
using System;
using System.Text.RegularExpressions;
using GrpcClient::EventStore.Client;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public static class Filter {
	public static readonly IEventFilter ExcludeSystemEvents = null;
	public static class StreamId {
		public static IEventFilter Prefix(string prefix) {
			throw new NotImplementedException();
		}

		public static IEventFilter Regex(Regex regex) {
			throw new NotImplementedException();
		}
	}

	public static class EventType {
		public static IEventFilter Prefix(string prefix) {
			throw new NotImplementedException();
		}

		public static IEventFilter Regex(Regex regex) {
			throw new NotImplementedException();
		}
	}
}
