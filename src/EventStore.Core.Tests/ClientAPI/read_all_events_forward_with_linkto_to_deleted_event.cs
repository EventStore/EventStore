using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class read_all_events_forward_with_linkto_to_deleted_event : SpecificationWithLinkToToDeletedEvents {
		private StreamEventsSlice _read;

		protected override void When() {
			_read = _conn.ReadStreamEventsForwardAsync(LinkedStreamName, 0, 1, true, null).Result;
		}

		[Test]
		public void one_event_is_read() {
			Assert.AreEqual(1, _read.Events.Length);
		}

		[Test]
		public void the_linked_event_is_not_resolved() {
			Assert.IsNull(_read.Events[0].Event);
		}

		[Test]
		public void the_link_event_is_included() {
			Assert.IsNotNull(_read.Events[0].OriginalEvent);
		}

		[Test]
		public void the_event_is_not_resolved() {
			Assert.IsFalse(_read.Events[0].IsResolved);
		}
	}
}
