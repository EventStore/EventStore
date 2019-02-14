using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using NUnit.Framework;
using System;
using System.Linq;

namespace EventStore.Core.Tests.Helpers.IODispatcherTests.QueueWriteEventsTests {
	[TestFixture]
	public class when_requesting_multiple_writes_with_different_keys : TestFixtureWithExistingEvents {
		protected override void Given() {
			_ioDispatcher.QueueWriteEvents(Guid.NewGuid(), $"stream-{Guid.NewGuid()}", ExpectedVersion.Any,
				new Event[] {new Event(Guid.NewGuid(), "event-type", false, string.Empty, string.Empty)},
				SystemAccount.Principal, (msg) => { });
			_ioDispatcher.QueueWriteEvents(Guid.NewGuid(), $"stream-{Guid.NewGuid()}", ExpectedVersion.Any,
				new Event[] {new Event(Guid.NewGuid(), "event-type", false, string.Empty, string.Empty)},
				SystemAccount.Principal, (msg) => { });
		}

		[Test]
		public void should_have_as_many_writes_in_flight_as_unique_keys() {
			Assert.AreEqual(2, _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Count());
		}
	}
}
