using System;

namespace EventStore.Client {
	public class EventStoreClientOperationOptions {
		public TimeSpan? TimeoutAfter { get; set; }
		public static EventStoreClientOperationOptions Default => new EventStoreClientOperationOptions {
			TimeoutAfter = TimeSpan.FromSeconds(5),
		};

		public EventStoreClientOperationOptions Clone() =>
			new EventStoreClientOperationOptions {
				TimeoutAfter = TimeoutAfter,
			};
	}
}
