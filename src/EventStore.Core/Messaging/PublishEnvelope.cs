using System;
using System.Diagnostics;
using System.Threading;
using EventStore.Core.Bus;

namespace EventStore.Core.Messaging {
	public class PublishEnvelope : IEnvelope {
		private readonly IPublisher _publisher;
		private readonly int _createdOnThread;
		private readonly string _createdOnThreadName;

		public PublishEnvelope(IPublisher publisher, bool crossThread = false) {
			_publisher = publisher;
			_createdOnThread = crossThread ? -1 : Thread.CurrentThread.ManagedThreadId;
			_createdOnThreadName = Thread.CurrentThread.Name;
		}

		public void ReplyWith<T>(T message) where T : Message {
			EnsureCorrectThread(message);
			_publisher.Publish(message);
		}

		[Conditional("DEBUG")]
		void EnsureCorrectThread(Message message) {
			if (_createdOnThread == -1 ||
				 Thread.CurrentThread.ManagedThreadId == _createdOnThread ||
				_publisher is IThreadSafePublisher) {
				return;
			}

			var publisher = _publisher is InMemoryBus bus
				? bus.Name :
				_publisher.GetType().Name;

			throw new InvalidOperationException(
				$"DEBUG: Publishing message \"{message}\" on the wrong thread. " + Environment.NewLine +
				$"Publisher: {publisher}" + Environment.NewLine +
				$"Expected thread: {_createdOnThread} \"{_createdOnThreadName}\"" + Environment.NewLine +
				$"Actual thread: {Thread.CurrentThread.ManagedThreadId} \"{Thread.CurrentThread.Name}\"");
		}
	}
}
