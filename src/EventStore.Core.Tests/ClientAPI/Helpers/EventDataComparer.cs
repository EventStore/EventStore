extern alias GrpcClient;
using System.Text;
using EventStore.Common.Utils;
using GrpcClient::EventStore.Client;

namespace EventStore.Core.Tests.ClientAPI.Helpers {
	internal static class EventDataComparer {
		public static bool Equal(EventData expected, EventRecord actual) {
			if (expected.EventId != actual.EventId)
				return false;

			if (expected.Type != actual.EventType)
				return false;

			var expectedDataString = Helper.UTF8NoBom.GetString(expected.Data.ToArray());
			var expectedMetadataString = Helper.UTF8NoBom.GetString(expected.Metadata.ToArray());

			var actualDataString = Helper.UTF8NoBom.GetString(actual.Data.ToArray());
			var actualMetadataDataString = Helper.UTF8NoBom.GetString(actual.Metadata.ToArray());

			return expectedDataString == actualDataString && expectedMetadataString == actualMetadataDataString;
		}

		public static bool Equal(EventData[] expected, EventRecord[] actual) {
			if (expected.Length != actual.Length)
				return false;

			for (var i = 0; i < expected.Length; i++) {
				if (!Equal(expected[i], actual[i]))
					return false;
			}

			return true;
		}
	}
}
