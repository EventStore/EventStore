using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.partition_state_update_manager {
	class FakeEventWriter : IEventWriter {
		private readonly List<EmittedEvent[]> _writes = new List<EmittedEvent[]>();

		public List<EmittedEvent[]> Writes {
			get { return _writes; }
		}

		public void ValidateOrderAndEmitEvents(EmittedEventEnvelope[] events) {
			Writes.Add(events.Select(v => v.Event).ToArray());
		}
	}
}
