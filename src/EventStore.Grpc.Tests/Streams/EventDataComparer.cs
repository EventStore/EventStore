using System;
using EventStore.Common.Utils;

namespace EventStore.Grpc.Streams {
	internal static class EventDataComparer {
		[Obsolete]
		public static bool Equal(EventData expected, EventRecord actual) {
			if (expected.EventId != actual.EventId)
				return false;

			if (expected.Type != actual.EventType)
				return false;

			var expectedDataString = Helper.UTF8NoBom.GetString(expected.Data ?? Array.Empty<byte>());
			var expectedMetadataString = Helper.UTF8NoBom.GetString(expected.Metadata ?? Array.Empty<byte>());

			var actualDataString = Helper.UTF8NoBom.GetString(actual.Data ?? Array.Empty<byte>());
			var actualMetadataDataString = Helper.UTF8NoBom.GetString(actual.Metadata ?? Array.Empty<byte>());

			return expectedDataString == actualDataString && expectedMetadataString == actualMetadataDataString;
		}

		[Obsolete]
		public static bool Equal(EventData[] expected, EventRecord[] actual) {
			if (expected.Length != actual.Length)
				return false;

			for (var i = 0; i < expected.Length; i++) {
				if (!Equal(expected[i], actual[i])) {
					return false;
				}
			}

			return true;
		}
	}
}
