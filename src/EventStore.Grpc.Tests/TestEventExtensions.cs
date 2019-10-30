using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Grpc.Tests {
	internal static class TestEventExtensions {
		public static IEnumerable<EventRecord> AsResolvedTestEvents(this IEnumerable<ResolvedEvent> events) {
			if (events == null) throw new ArgumentNullException(nameof(events));
			return events.Where(x => x.Event.EventType == EventStoreGrpcFixture.TestEventType).Select(x => x.Event);
		}
	}
}
