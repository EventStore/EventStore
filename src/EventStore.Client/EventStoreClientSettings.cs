using System;
using System.Net.Http;
using Grpc.Core.Interceptors;

namespace EventStore.Client {
	public class EventStoreClientSettings {
		public Uri Address { get; set; }
		public Interceptor[] Interceptors { get; set; } = Array.Empty<Interceptor>();
		public string ConnectionName { get; set; }
		public Func<HttpClient> CreateHttpClient { get; set; }

		public EventStoreClientOperationOptions OperationOptions { get; set; } = EventStoreClientOperationOptions.Default;

		public EventStoreClientSettings(Uri address) {
			if (address == null) throw new ArgumentNullException(nameof(address));
			Address = address;
		}
	}
}
