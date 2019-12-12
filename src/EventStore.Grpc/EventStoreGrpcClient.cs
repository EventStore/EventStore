using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using EventStore.Grpc.PersistentSubscriptions;
using EventStore.Grpc.Projections;
using EventStore.Grpc.Users;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using ReadReq = EventStore.Grpc.Streams.ReadReq;

namespace EventStore.Grpc {
	public class EventStoreGrpcClientSettings {
		public Uri Address { get; set; }
		public Interceptor[] Interceptors { get; set; } = Array.Empty<Interceptor>();
		public string ConnectionName { get; set; } = $"ES-{Guid.NewGuid()}";
		public Func<HttpClient> CreateHttpClient { get; set; }

		public EventStoreGrpcClientSettings(Uri address) {
			if (address == null) throw new ArgumentNullException(nameof(address));
			Address = address;
		}
	}

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

		public EventStoreGrpcClient(EventStoreGrpcClientSettings settings = null) {
			settings ??= new EventStoreGrpcClientSettings(new UriBuilder {
				Scheme = Uri.UriSchemeHttps,
				Port = 2113
			}.Uri);
			_channel = GrpcChannel.ForAddress(settings.Address, new GrpcChannelOptions {
				HttpClient = settings.CreateHttpClient?.Invoke()
			});
			var callInvoker = settings.Interceptors.Aggregate(
				_channel.CreateCallInvoker().Intercept(new TypedExceptionInterceptor()),
				(invoker, interceptor) => invoker.Intercept(interceptor));
			_client = new Streams.Streams.StreamsClient(callInvoker);
			PersistentSubscriptions = new EventStorePersistentSubscriptionsGrpcClient(callInvoker);
			ProjectionsManager = new EventStoreProjectionManagerGrpcClient(callInvoker);
			UsersManager = new EventStoreUserManagerGrpcClient(callInvoker);
		}

		public EventStoreGrpcClient(Uri address, Func<HttpClient> createHttpClient = default) : this(
			new EventStoreGrpcClientSettings(address) {
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
