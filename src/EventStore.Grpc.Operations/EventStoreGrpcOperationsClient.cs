using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;

namespace EventStore.Grpc.Operations {
	public partial class EventStoreGrpcOperationsClient : IDisposable {
		private readonly GrpcChannel _channel;
		private readonly Operations.OperationsClient _client;

		public EventStoreGrpcOperationsClient(Uri address, Func<HttpClient> createHttpClient = default) {
			if (address == null) {
				throw new ArgumentNullException(nameof(address));
			}

			_channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions {
				HttpClient = createHttpClient?.Invoke(),
			});
			var callInvoker = _channel.CreateCallInvoker().Intercept(new TypedExceptionInterceptor());
			_client = new Operations.OperationsClient(callInvoker);
		}

		public void Dispose() {
			_channel?.Dispose();
		}
	}
}
