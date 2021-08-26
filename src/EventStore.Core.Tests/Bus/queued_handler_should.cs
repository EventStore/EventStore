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
				() => QueuedHandler.CreateQueuedHandler(null, "throwing", new QueueStatsManager(), watchSlowMsg: false));
		}

		[Test]
		public void throw_if_name_is_null() {
			Assert.Throws<ArgumentNullException>(
				() => QueuedHandler.CreateQueuedHandler(Consumer, null, new QueueStatsManager(), watchSlowMsg: false));
		}
	}

	[TestFixture]
	public class queued_handler_channel_should : queued_handler_should {
		public queued_handler_channel_should()
			: base((consumer, name, timeout) => new QueuedHandlerChannel(consumer, name, new QueueStatsManager(),false, null, timeout)) {
		}
	}

	//[TestFixture]
	//public class queued_handler_threadpool_should : queued_handler_should {
	//	public queued_handler_threadpool_should()
	//		: base((consumer, name, timeout) => new QueuedHandlerThreadPool(consumer, name, new QueueStatsManager(),false, null, timeout)) {
	//	}
	//}
}
