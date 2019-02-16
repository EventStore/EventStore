using System;

namespace EventStore.Core.Services.PersistentSubscription {
	public struct RetryableMessage {
		public readonly Guid MessageId;
		public readonly DateTime DueTime;

		public RetryableMessage(Guid messageId, DateTime dueTime) {
			MessageId = messageId;
			DueTime = dueTime;
		}
	}
}
