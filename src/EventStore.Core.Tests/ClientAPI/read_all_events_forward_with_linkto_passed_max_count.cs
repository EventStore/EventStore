using System.Collections.Generic;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class read_all_events_forward_with_linkto_passed_max_count : SpecificationWithLinkToToMaxCountDeletedEvents {
		private StreamEventsSlice _read;

		protected override void When() {
			_read = _conn.ReadStreamEventsForwardAsync(LinkedStreamName, 0, 1, true).Result;
		}

		[Test]
		public void one_event_is_read() {
			Assert.AreEqual(1, _read.Events.Length);
		}
	}
}
