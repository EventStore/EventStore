using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using NUnit.Framework;

namespace EventStore.Core.Tests.Bus.Helpers {
	public abstract class QueuedHandlerTestWithNoopConsumer {
		private readonly Func<IHandle<Message>, string, TimeSpan, IQueuedHandler> _queuedHandlerFactory;

		protected IQueuedHandler Queue;
		protected IHandle<Message> Consumer;

		protected QueuedHandlerTestWithNoopConsumer(
			Func<IHandle<Message>, string, TimeSpan, IQueuedHandler> queuedHandlerFactory) {
			Ensure.NotNull(queuedHandlerFactory, "queuedHandlerFactory");
			_queuedHandlerFactory = queuedHandlerFactory;
		}

		[SetUp]
		public virtual void SetUp() {
			Consumer = new NoopConsumer();
			Queue = _queuedHandlerFactory(Consumer, "test_name", TimeSpan.FromMilliseconds(5000));
		}

		[TearDown]
		public virtual void TearDown() {
			Queue.Stop();
			Queue = null;
			Consumer = null;
		}
	}
}
