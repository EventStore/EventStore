using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using EventStore.Client.Logging;
using EventStore.Client.PersistentSubscriptions;
using EventStore.Client.Projections;
using EventStore.Client.Users;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReadReq = EventStore.Client.Streams.ReadReq;

namespace EventStore.Client {
	public partial class EventStoreClient : IDisposable {
		private static readonly JsonSerializerOptions StreamMetadataJsonSerializerOptions = new JsonSerializerOptions {
			Converters = {
				StreamMetadataJsonConverter.Instance
			},
		};

		private static readonly ILogger Log = LogProvider.CreateLogger<EventStoreClient>();

		private readonly EventStoreClientSettings _settings;
		private readonly GrpcChannel _channel;
		private readonly Streams.Streams.StreamsClient _client;
		public EventStorePersistentSubscriptionsClient PersistentSubscriptions { get; }
		public EventStoreProjectionManagerClient ProjectionsManager { get; }
		public EventStoreUserManagerClient UsersManager { get; }

		public EventStoreClient(IOptions<EventStoreClientSettings> options) : this(options.Value) {
		}
		
		public EventStoreClient(EventStoreClientSettings settings = null) {
			_settings = settings ??= new EventStoreClientSettings();
			Action<Exception> exceptionNotificationHook = null;
			var httpHandler = settings.CreateHttpMessageHandler?.Invoke() ?? new HttpClientHandler();
			if (settings.ConnectivitySettings.GossipSeeds.Length > 0) {
				ConfigureClusterAwareHandler();
			}
			
			_channel = GrpcChannel.ForAddress(settings.ConnectivitySettings.Address, new GrpcChannelOptions {
				HttpClient = new HttpClient(httpHandler) {
					Timeout = Timeout.InfiniteTimeSpan,
					DefaultRequestVersion = new Version(2, 0),
				},
				LoggerFactory = LogProvider.LoggerFactory
			});
			var connectionName = settings.ConnectionName ?? $"ES-{Guid.NewGuid()}";

			var callInvoker = settings.Interceptors.Aggregate(
				_channel.CreateCallInvoker()
					.Intercept(new TypedExceptionInterceptor(exceptionNotificationHook))
					.Intercept(new ConnectionNameInterceptor(connectionName)),
				(invoker, interceptor) => invoker.Intercept(interceptor));
			_client = new Streams.Streams.StreamsClient(callInvoker);
			PersistentSubscriptions = new EventStorePersistentSubscriptionsClient(callInvoker);
			ProjectionsManager = new EventStoreProjectionManagerClient(callInvoker);
			UsersManager = new EventStoreUserManagerClient(callInvoker);

			void ConfigureClusterAwareHandler()
			{
				var clusterAwareHttpHandler = new ClusterAwareHttpHandler(
					settings.ConnectivitySettings.NodePreference == NodePreference.Leader,
					new ClusterEndpointDiscoverer(
						settings.ConnectivitySettings.MaxDiscoverAttempts,
						settings.ConnectivitySettings.GossipSeeds,
						settings.ConnectivitySettings.GossipTimeout,
						settings.ConnectivitySettings.DiscoveryInterval,
						settings.ConnectivitySettings.NodePreference)) {
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
				options.Count = new ReadReq.Types.Empty();
			}

			options.CheckpointIntervalMultiplier = filterOptions.CheckpointInterval;

			return options;
		}
	}
}
