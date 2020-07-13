using EventStore.Core.Data;
using EventStore.Core.Services.UserManagement;
using NUnit.Framework;
using System;
using EventStore.Core.TransactionLog.Data;

namespace EventStore.Core.Tests.Helpers.IODispatcherTests.QueueWriteEventsTests {
	[TestFixture]
	public class when_a_write_completes : TestFixtureWithExistingEvents {
		private bool _completed = false;

		protected override void Given() {
			AllWritesQueueUp();

			_ioDispatcher.QueueWriteEvents(Guid.NewGuid(), $"stream-{Guid.NewGuid()}", ExpectedVersion.Any,
				new Event[] {new Event(Guid.NewGuid(), "event-type", false, string.Empty, string.Empty)},
				SystemAccounts.System, (msg) => { _completed = true; });
			OneWriteCompletes();
		}

		[Test]
		public void should_invoke_callback_when_write_completes() {
			Assert.IsTrue(_completed);
		}
	}
}
