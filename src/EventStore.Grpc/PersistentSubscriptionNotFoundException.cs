using System;

namespace EventStore.Grpc {
	public class PersistentSubscriptionNotFoundException : Exception {
		public readonly string StreamName;
		public readonly string GroupName;

		public PersistentSubscriptionNotFoundException(string streamName, string groupName, Exception exception = null)
			: base($"Subscription group '{groupName}' on stream '{streamName}' does not exist.", exception) {
			if (streamName == null) throw new ArgumentNullException(nameof(streamName));
			if (groupName == null) throw new ArgumentNullException(nameof(groupName));
			StreamName = streamName;
			GroupName = groupName;
		}
	}
	
	public class PersistentSubscriptionDroppedByServerException : Exception {
		public readonly string StreamName;
		public readonly string GroupName;

		public PersistentSubscriptionDroppedByServerException(string streamName, string groupName, Exception exception = null)
			: base($"Subscription group '{groupName}' on stream '{streamName}' was dropped.", exception) {
			if (streamName == null) throw new ArgumentNullException(nameof(streamName));
			if (groupName == null) throw new ArgumentNullException(nameof(groupName));
			StreamName = streamName;
			GroupName = groupName;
		}
	}

}
