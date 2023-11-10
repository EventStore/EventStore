using EventStore.ClientAPI;

namespace EventStore.Core.Tests.ClientAPI;

public class GrpcEventStoreSubscription : EventStoreSubscription {
	public GrpcEventStoreSubscription(string streamId, long lastCommitPosition, long? lastEventNumber) : base(streamId, lastCommitPosition, lastEventNumber)
	{
	}

	public override void Unsubscribe() {
		throw new System.NotImplementedException();
	}
}
