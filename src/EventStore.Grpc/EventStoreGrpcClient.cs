using System;
using System.Net.Http;
using System.Text.Json;
using EventStore.Grpc.PersistentSubscriptions;
using EventStore.Grpc.Projections;
using EventStore.Grpc.Users;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using ReadReq = EventStore.Grpc.Streams.ReadReq;

namespace EventStore.Grpc {
	public partial class EventStoreGrpcClient : IDisposable {
		private static readonly JsonSerializerOptions StreamMetadataJsonSerializerOptions = new JsonSerializerOptions {
			Converters = {
				StreamMetadataJsonConverter.Instance
			},
		};

		private readonly GrpcChannel _channel;
		private readonly Streams.Streams.StreamsClient _client;
		public EventStorePersistentSubscriptionsGrpcClient PersistentSubscriptions { get; }
		public EventStoreProjectionManagerGrpcClient ProjectionsManager { get; }
		public EventStoreUserManagerGrpcClient UsersManager { get; }

		public EventStoreGrpcClient(Uri address, Func<HttpClient> createHttpClient = default) {
			if (address == null) {
				throw new ArgumentNullException(nameof(address));
			}

			_channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions {
				HttpClient = createHttpClient?.Invoke(),
			});
			var callInvoker = _channel.CreateCallInvoker().Intercept(new TypedExceptionInterceptor());
			_client = new Streams.Streams.StreamsClient(callInvoker);
			PersistentSubscriptions = new EventStorePersistentSubscriptionsGrpcClient(callInvoker);
			ProjectionsManager = new EventStoreProjectionManagerGrpcClient(callInvoker);
			UsersManager = new EventStoreUserManagerGrpcClient(callInvoker);
		}

		public void Dispose() => _channel.Dispose();

		private static ReadReq.Types.Options.Types.FilterOptions GetFilterOptions(IEventFilter filter) {
			if (filter == null) {
				return null;
			}

			var options = filter switch {
				StreamFilter _ => new ReadReq.Types.Options.Types.FilterOptions {
					StreamName = (filter.Prefixes, filter.Regex) switch {
						(PrefixFilterExpression[] _, RegularFilterExpression _)
						when (filter.Prefixes?.Length ?? 0) == 0 &&
						     filter.Regex != RegularFilterExpression.None =>
						new ReadReq.Types.Options.Types.FilterOptions.Types.Expression
							{Regex = filter.Regex},
						(PrefixFilterExpression[] _, RegularFilterExpression _)
						when (filter.Prefixes?.Length ?? 0) != 0 &&
						     filter.Regex == RegularFilterExpression.None =>
						new ReadReq.Types.Options.Types.FilterOptions.Types.Expression {
							Prefix = {Array.ConvertAll(filter.Prefixes, e => (string)e)}
						},
						_ => throw new InvalidOperationException()
					}
				},
				EventTypeFilter _ => new ReadReq.Types.Options.Types.FilterOptions {
					EventType = (filter.Prefixes, filter.Regex) switch {
						(PrefixFilterExpression[] _, RegularFilterExpression _)
						when (filter.Prefixes?.Length ?? 0) == 0 &&
						     filter.Regex != RegularFilterExpression.None =>
						new ReadReq.Types.Options.Types.FilterOptions.Types.Expression
							{Regex = filter.Regex},
						(PrefixFilterExpression[] _, RegularFilterExpression _)
						when (filter.Prefixes?.Length ?? 0) != 0 &&
						     filter.Regex == RegularFilterExpression.None =>
						new ReadReq.Types.Options.Types.FilterOptions.Types.Expression {
							Prefix = {Array.ConvertAll(filter.Prefixes, e => (string)e)}
						},
						_ => throw new InvalidOperationException()
					}
				},
				_ => throw new InvalidOperationException()
			};

			if (filter.MaxSearchWindow.HasValue) {
				options.Max = filter.MaxSearchWindow.Value;
			} else {
				options.Count = new ReadReq.Types.Empty();
			}

			return options;
		}
	}
}
