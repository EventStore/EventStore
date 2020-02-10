using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using EventStore.Client.PersistentSubscriptions;
using EventStore.Client.Projections;
using EventStore.Client.Users;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using ReadReq = EventStore.Client.Streams.ReadReq;

namespace EventStore.Client {
	public partial class EventStoreClient : IDisposable {
		private static readonly JsonSerializerOptions StreamMetadataJsonSerializerOptions = new JsonSerializerOptions {
			Converters = {
				StreamMetadataJsonConverter.Instance
			},
		};

		private readonly EventStoreClientSettings _settings;
		private readonly GrpcChannel _channel;
		private readonly Streams.Streams.StreamsClient _client;
		public EventStorePersistentSubscriptionsClient PersistentSubscriptions { get; }
		public EventStoreProjectionManagerClient ProjectionsManager { get; }
		public EventStoreUserManagerClient UsersManager { get; }

		public EventStoreClient(EventStoreClientSettings settings = null) {
			_settings = settings ??= new EventStoreClientSettings(new UriBuilder {
				Scheme = Uri.UriSchemeHttps,
				Port = 2113
			}.Uri);
			_channel = GrpcChannel.ForAddress(settings.Address, new GrpcChannelOptions {
				HttpClient = settings.CreateHttpClient?.Invoke()
			});
			var connectionName = settings.ConnectionName ?? $"ES-{Guid.NewGuid()}";

			var callInvoker = settings.Interceptors.Aggregate(
				_channel.CreateCallInvoker()
					.Intercept(new TypedExceptionInterceptor())
					.Intercept(new ConnectionNameInterceptor(connectionName)),
				(invoker, interceptor) => invoker.Intercept(interceptor));
			_client = new Streams.Streams.StreamsClient(callInvoker);
			PersistentSubscriptions = new EventStorePersistentSubscriptionsClient(callInvoker);
			ProjectionsManager = new EventStoreProjectionManagerClient(callInvoker);
			UsersManager = new EventStoreUserManagerClient(callInvoker);
		}

		public EventStoreClient(Uri address, Func<HttpClient> createHttpClient = default) : this(
			new EventStoreClientSettings(address) {
				CreateHttpClient = createHttpClient
			}) {
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
