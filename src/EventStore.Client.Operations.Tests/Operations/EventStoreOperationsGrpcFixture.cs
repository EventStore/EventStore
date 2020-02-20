using System;

namespace EventStore.Client.Operations {
	public abstract class EventStoreOperationsGrpcFixture : EventStoreGrpcFixture {
		public EventStoreOperationsClient OperationsClient { get; }

		protected EventStoreOperationsGrpcFixture() {
			OperationsClient = new EventStoreOperationsClient(new UriBuilder().Uri, () => new ResponseVersionHandler {
				InnerHandler = TestServer.CreateHandler()
			});
		}
	}
}
