extern alias GrpcClient;
using System;
using EventStore.Common.Utils;
using GrpcClient::EventStore.Client;

namespace EventStore.Core.Tests.ClientAPI.Helpers {
	public class TestEvent {
		public static EventData NewTestEvent(string data = null, string metadata = null, string eventName = "TestEvent") {
			return NewTestEvent(Guid.NewGuid(), data, metadata, eventName);
		}

		public static EventData NewTestEvent(Guid eventId, string data = null, string metadata = null, string eventName = "TestEvent") {
			var encodedData = Helper.UTF8NoBom.GetBytes(data ?? eventId.ToString());
			var encodedMetadata = Helper.UTF8NoBom.GetBytes(metadata ?? "metadata");

			return new EventData(Uuid.FromGuid(eventId), eventName, encodedData, encodedMetadata);
		}
	}
}
