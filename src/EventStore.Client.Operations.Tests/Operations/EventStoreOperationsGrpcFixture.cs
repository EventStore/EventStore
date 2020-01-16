using System;
using System.Net.Http;
using System.Threading;

namespace EventStore.Client.Operations {
	public abstract class EventStoreOperationsGrpcFixture : EventStoreGrpcFixture {
		public EventStoreGrpcOperationsClient OperationsClient { get; }

		protected EventStoreOperationsGrpcFixture() {
			OperationsClient = new EventStoreGrpcOperationsClient(new UriBuilder().Uri, () =>
				new HttpClient(new ResponseVersionHandler {
					InnerHandler = TestServer.CreateHandler()
				}) {
					Timeout = Timeout.InfiniteTimeSpan
				});
		}
	}
}
