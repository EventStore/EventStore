using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using EventStore.Client.Interceptors;
using EventStore.Client.PersistentSubscriptions;
using EventStore.Client.Projections;
using EventStore.Client.Shared;
using EventStore.Client.Users;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
		private readonly ILogger<EventStoreClient> _log;

		public EventStorePersistentSubscriptionsClient PersistentSubscriptions { get; }
		public EventStoreProjectionManagerClient ProjectionsManager { get; }
		public EventStoreUserManagerClient UsersManager { get; }

		public EventStoreClient(IOptions<EventStoreClientSettings> options) : this(options.Value) {
		}

		public EventStoreClient(EventStoreClientSettings settings = null) {
			_settings = settings ?? new EventStoreClientSettings();
			var connectionName = _settings.ConnectionName ?? $"ES-{Guid.NewGuid()}";
			Action<Exception> exceptionNotificationHook = null;
			var httpHandler = _settings.CreateHttpMessageHandler?.Invoke() ?? new HttpClientHandler();
			if (_settings.ConnectivitySettings.GossipSeeds.Length > 0) {
				ConfigureClusterAwareHandler();
			}

			_channel = GrpcChannel.ForAddress(_settings.ConnectivitySettings.Address, new GrpcChannelOptions {
				HttpClient = new HttpClient(httpHandler) {
					Timeout = Timeout.InfiniteTimeSpan,
					DefaultRequestVersion = new Version(2, 0),
				},
				LoggerFactory = settings.LoggerFactory
			});
			var callInvoker = (_settings.Interceptors ?? Array.Empty<Interceptor>()).Aggregate(
				_channel.CreateCallInvoker()
					.Intercept(new TypedExceptionInterceptor(exceptionNotificationHook))
					.Intercept(new ConnectionNameInterceptor(connectionName)),
				(invoker, interceptor) => invoker.Intercept(interceptor));
			_client = new Streams.Streams.StreamsClient(callInvoker);
			PersistentSubscriptions = new EventStorePersistentSubscriptionsClient(callInvoker, _settings);
			ProjectionsManager = new EventStoreProjectionManagerClient(callInvoker);
			UsersManager = new EventStoreUserManagerClient(callInvoker);
			_log = _settings.LoggerFactory?.CreateLogger<EventStoreClient>() ?? new NullLogger<EventStoreClient>();

			void ConfigureClusterAwareHandler() {
				var clusterAwareHttpHandler = new ClusterAwareHttpHandler(
					_settings.ConnectivitySettings.NodePreference == NodePreference.Leader,
					new ClusterEndpointDiscoverer(
						_settings.ConnectivitySettings.MaxDiscoverAttempts,
						_settings.ConnectivitySettings.GossipSeeds,
						_settings.ConnectivitySettings.GossipTimeout,
						_settings.ConnectivitySettings.DiscoveryInterval,
						_settings.ConnectivitySettings.NodePreference)) {
					InnerHandler = httpHandler
				};
				exceptionNotificationHook = clusterAwareHttpHandler.ExceptionOccurred;
				httpHandler = clusterAwareHttpHandler;
			}
		}

		public void Dispose() => _channel.Dispose();

		private static ReadReq.Types.Options.Types.FilterOptions GetFilterOptions(FilterOptions filterOptions) {
			if (filterOptions == null) {
				return null;
			}

			var filter = filterOptions.Filter;

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
				options.Count = new Empty();
			}

			options.CheckpointIntervalMultiplier = filterOptions.CheckpointInterval;

			return options;
		}
	}
}
