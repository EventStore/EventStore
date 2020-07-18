namespace EventStore.Core.Messages {
	public interface IQueueAffineMessage {
		int QueueId { get; }
	}
}
