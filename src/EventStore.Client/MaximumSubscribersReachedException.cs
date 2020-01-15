using System;

namespace EventStore.Client {
	public class MaximumSubscribersReachedException : Exception {
		public readonly string StreamName;
		public readonly string GroupName;

		public MaximumSubscribersReachedException(string streamName, string groupName, Exception exception = null)
			: base($"Maximum subscriptions reached for subscription group '{groupName}' on stream '{streamName}.'", exception) {
			if (streamName == null) throw new ArgumentNullException(nameof(streamName));
			if (groupName == null) throw new ArgumentNullException(nameof(groupName));
			StreamName = streamName;
			GroupName = groupName;
		}
	}
}
