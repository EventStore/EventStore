using System;

namespace EventStore.Core.Services.PersistentSubscription {
	public interface IPersistentSubscriptionCheckpointWriter {
		void BeginWriteState(IPersistentSubscriptionStreamPosition state);
		void BeginDelete(Action<IPersistentSubscriptionCheckpointWriter> completed);
	}
}
