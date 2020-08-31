using EventStore.Core.Messaging;

namespace EventStore.Core.Bus {
	/// <summary>
	/// A common interface for single consumer queues (**SC).
	/// </summary>
	public interface ISingleConsumerMessageQueue {
		void Enqueue(Message item);
		bool TryDequeue(Message[] segment, out QueueBatchDequeueResult result);
	}
}
