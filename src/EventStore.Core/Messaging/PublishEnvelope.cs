using System.Diagnostics;
using System.Threading;
using EventStore.Core.Bus;

namespace EventStore.Core.Messaging {
	public class PublishEnvelope : IEnvelope {
		private readonly IPublisher _publisher;
		private readonly int _createdOnThread;

		public PublishEnvelope(IPublisher publisher, bool crossThread = false) {
			_publisher = publisher;
			_createdOnThread = crossThread ? -1 : Thread.CurrentThread.ManagedThreadId;
		}

		public void ReplyWith<T>(T message) where T : Message {
			Debug.Assert(_createdOnThread == -1 ||
			             Thread.CurrentThread.ManagedThreadId == _createdOnThread ||
			             _publisher is IThreadSafePublisher);
			_publisher.Publish(message);
		}
	}
}
