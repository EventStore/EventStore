using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using ReadReq = EventStore.Grpc.Streams.ReadReq;

namespace EventStore.Grpc {
	public partial class EventStoreGrpcClient : IDisposable {
		private readonly Streams.Streams.StreamsClient _client;

		private static readonly JsonSerializerOptions StreamMetadataJsonSerializerOptions = new JsonSerializerOptions {
			Converters = {
				StreamMetadataJsonConverter.Instance
			},
		};

		private readonly GrpcChannel _channel;

		public EventStoreGrpcClient(Uri address, Func<HttpClient> createHttpClient = default) {
			_channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions {
				HttpClient = createHttpClient?.Invoke(),
			});
			_client = new Streams.Streams.StreamsClient(_channel.CreateCallInvoker()
				.Intercept(new TypedExceptionInterceptor()));
		}

		private static Metadata GetRequestMetadata(UserCredentials userCredentials) =>
			userCredentials == null
				? null
				: new Metadata {
					new Metadata.Entry(Constants.Headers.Authorization, new AuthenticationHeaderValue(
							Constants.Headers.BasicScheme,
							Convert.ToBase64String(
								Encoding.ASCII.GetBytes($"{userCredentials.Username}:{userCredentials.Password}")))
						.ToString())
				};


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
