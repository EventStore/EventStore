namespace EventStore.Core.Bus {
	/// <summary>
	/// A struct providing information for <see cref="ISingleConsumerMessageQueue.TryDequeue"/> result.
	/// </summary>
	public struct QueueBatchDequeueResult {
		public int DequeueCount;
		public int EstimateCurrentQueueCount;
	}
}
