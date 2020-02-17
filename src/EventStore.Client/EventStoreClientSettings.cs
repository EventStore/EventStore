using System;
using System.Net.Http;
using Grpc.Core.Interceptors;

namespace EventStore.Client {
	public class EventStoreClientSettings {
		public Interceptor[] Interceptors { get; set; } = Array.Empty<Interceptor>();
		public string ConnectionName { get; set; }
		public Func<HttpMessageHandler> CreateHttpMessageHandler { get; set; }

		public EventStoreClientOperationOptions OperationOptions { get; set; } =
			EventStoreClientOperationOptions.Default;

		public EventStoreClientConnectivitySettings ConnectivitySettings { get; set; } =
			EventStoreClientConnectivitySettings.Default;
	}
}
