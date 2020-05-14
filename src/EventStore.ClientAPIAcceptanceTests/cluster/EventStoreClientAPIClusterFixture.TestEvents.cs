using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.ClientAPI.Tests {
	partial class EventStoreClientAPIFixture {
		private const string TestEventType = "-";

		public IEnumerable<EventData> CreateTestEvents(int count = 1)
			=> Enumerable.Range(0, count).Select(CreateTestEvent);

		protected static EventData CreateTestEvent(int index) =>
			new EventData(Guid.NewGuid(), TestEventType, true, Encoding.UTF8.GetBytes($@"{{""x"":{index}}}"),
				Array.Empty<byte>());
	}
}
