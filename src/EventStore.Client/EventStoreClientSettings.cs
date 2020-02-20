using System;
using System.Collections.Generic;
using System.Net.Http;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace EventStore.Client {
	public class EventStoreClientSettings {
		public IEnumerable<Interceptor> Interceptors { get; set; }
		public string ConnectionName { get; set; }
		public Func<HttpMessageHandler> CreateHttpMessageHandler { get; set; }
		public ILoggerFactory LoggerFactory { get; set; }

		public EventStoreClientOperationOptions OperationOptions { get; set; } =
			EventStoreClientOperationOptions.Default;

		public EventStoreClientConnectivitySettings ConnectivitySettings { get; set; } =
			EventStoreClientConnectivitySettings.Default;
	}
}
