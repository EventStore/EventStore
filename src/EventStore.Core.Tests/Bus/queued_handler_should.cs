using System;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Bus.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus {
	[TestFixture]
	public abstract class queued_handler_should : QueuedHandlerTestWithNoopConsumer {
		protected queued_handler_should(Func<IHandle<Message>, string, TimeSpan, IQueuedHandler> queuedHandlerFactory)
			: base(queuedHandlerFactory) {
		}

		[Test]
		public void throw_if_handler_is_null() {
			Assert.Throws<ArgumentNullException>(
				() => QueuedHandler.CreateQueuedHandler(null, "throwing", watchSlowMsg: false));
		}

		[Test]
		public void throw_if_name_is_null() {
			Assert.Throws<ArgumentNullException>(
				() => QueuedHandler.CreateQueuedHandler(Consumer, null, watchSlowMsg: false));
		}
	}

	[TestFixture]
	public class queued_handler_mres_should : queued_handler_should {
		public queued_handler_mres_should()
			: base((consumer, name, timeout) => new QueuedHandlerMresWithMpsc(consumer, name, false, null, timeout)) {
		}
	}

	[TestFixture]
	public class queued_handler_autoreset_should : queued_handler_should {
		public queued_handler_autoreset_should()
			: base((consumer, name, timeout) => new QueuedHandlerAutoResetWithMpsc(consumer, name, false, null, timeout)
			) {
		}
	}

	[TestFixture]
	public class queued_handler_sleep_should : queued_handler_should {
		public queued_handler_sleep_should()
			: base((consumer, name, timeout) => new QueuedHandlerSleep(consumer, name, false, null, timeout)) {
		}
	}

	[TestFixture]
	public class queued_handler_pulse_should : queued_handler_should {
		public queued_handler_pulse_should()
			: base((consumer, name, timeout) => new QueuedHandlerPulse(consumer, name, false, null, timeout)) {
		}
	}

	[TestFixture]
	public class queued_handler_threadpool_should : queued_handler_should {
		public queued_handler_threadpool_should()
			: base((consumer, name, timeout) => new QueuedHandlerThreadPool(consumer, name, false, null, timeout)) {
		}
	}
}
