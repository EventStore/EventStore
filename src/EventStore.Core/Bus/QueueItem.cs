using EventStore.Core.Messaging;
using EventStore.Core.Time;

namespace EventStore.Core.Bus;

public struct QueueItem {
	public QueueItem(Instant enqueuedAt, Message message) {
		EnqueuedAt = enqueuedAt;
		Message = message;
	}

	public Instant EnqueuedAt { get; }
	public Message Message { get; }
}
