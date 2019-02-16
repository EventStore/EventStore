using System;

namespace EventStore.Core.Services.PersistentSubscription {
	public interface IPersistentSubscriptionCheckpointWriter {
		void BeginWriteState(long state);
		void BeginDelete(Action<IPersistentSubscriptionCheckpointWriter> completed);
	}
}
