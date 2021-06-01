using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.Streams;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Tests.Helpers;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Convert = System.Convert;
using Streams = EventStore.Client.Streams.Streams;

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests {
	public abstract class GrpcSpecification<TLogFormat, TStreamId> : IDisposable {
		private readonly TestServer _server;
		private readonly GrpcChannel _channel;
		private readonly IHost _host;
		private readonly MiniNode<TLogFormat, TStreamId> _node;
		protected MiniNode<TLogFormat, TStreamId> Node => _node;
		internal Streams.StreamsClient StreamsClient { get; }
		private readonly BatchAppender _batchAppender;

		protected GrpcSpecification() {
			_node = new MiniNode<TLogFormat, TStreamId>(GetType().FullName, inMemDb: true);
			var builder = new HostBuilder()
				.ConfigureWebHostDefaults(webHost => webHost.UseTestServer()
					.ConfigureServices(services => _node.Node.Startup.ConfigureServices(services))
					.Configure(_node.Node.Startup.Configure));
			_host = builder.Start();
			_server = _host.GetTestServer();
			_channel = GrpcChannel.ForAddress(new UriBuilder {
				Scheme = Uri.UriSchemeHttps
			}.Uri, new GrpcChannelOptions {
				HttpClient = _server.CreateClient(),
				DisposeHttpClient = true
			});
			StreamsClient = new Streams.StreamsClient(_channel);
			_batchAppender = new BatchAppender(StreamsClient);
		}

		protected abstract Task Given();

		protected abstract Task When();

		[OneTimeSetUp]
		public async Task SetUp() {
			await _node.Start();
			await _node.AdminUserCreated;
			_batchAppender.Start();
			try {
				await Given().WithTimeout(TimeSpan.FromSeconds(10));
			} catch (Exception ex) {
				throw new Exception("Given Failed", ex);
			}

			await When().WithTimeout(TimeSpan.FromSeconds(10));
		}

		private static CallCredentials CallCredentialsFromUser((string userName, string password) credentials) =>
			CallCredentials.FromInterceptor((_, metadata) => {
				metadata.Add(new Metadata.Entry("authorization",
					$"Basic {Convert.ToBase64String(Encoding.ASCII.GetBytes($"{credentials.userName}:{credentials.password}"))}"));

				return Task.CompletedTask;
			});

		protected static (string userName, string password) AdminCredentials => ("admin", "changeit");

		protected virtual (string userName, string password) DefaultCredentials => default;

		protected CallOptions GetCallOptions((string userName, string password) credentials = default) =>
			new(credentials: GetCredentials(credentials == default ? DefaultCredentials : credentials),
				deadline: Debugger.IsAttached
					? DateTime.UtcNow.AddDays(1)
					: new DateTime?());

		private static CallCredentials GetCredentials((string userName, string password) credentials) =>
			credentials == default ? null : CallCredentialsFromUser(credentials);

		internal ValueTask<BatchAppendResp> AppendToStreamBatch(params BatchAppendReq[] requests) =>
			_batchAppender.Call(requests);

		internal static IEnumerable<BatchAppendReq.Types.ProposedMessage> CreateEvents(int count) =>
			Enumerable.Range(0, count)
				.Select(_ => new BatchAppendReq.Types.ProposedMessage {
					Data = ByteString.Empty,
					Id = Uuid.NewUuid().ToDto(),
					CustomMetadata = ByteString.Empty,
					Metadata = {
						{Core.Services.Transport.Grpc.Constants.Metadata.ContentType, "application/octet-stream"},
						{Core.Services.Transport.Grpc.Constants.Metadata.Type, "-"}
					}
				});

		public void Dispose() {
			_batchAppender.DisposeAsync().GetAwaiter().GetResult();
			_server?.Dispose();
			_channel?.Dispose();
			_host?.Dispose();
		}

		private class BatchAppender : IAsyncDisposable {
			private readonly Lazy<AsyncDuplexStreamingCall<BatchAppendReq, BatchAppendResp>> _batchAppendLazy;
			private AsyncDuplexStreamingCall<BatchAppendReq, BatchAppendResp> BatchAppend => _batchAppendLazy.Value;
			private readonly ConcurrentDictionary<Uuid, TaskCompletionSource<BatchAppendResp>> _responses;

			public BatchAppender(Streams.StreamsClient streamsClient) {
				_batchAppendLazy = new Lazy<AsyncDuplexStreamingCall<BatchAppendReq, BatchAppendResp>>(() =>
					streamsClient.BatchAppend(new CallOptions(credentials: GetCredentials(default),
						deadline: DateTime.UtcNow.AddDays(1))));
				_responses = new();
			}

			public void Start() =>
				Task.Run(async () => {
					try {
						while (await BatchAppend.ResponseStream.MoveNext().ConfigureAwait(false)) {
							var response = BatchAppend.ResponseStream.Current;
							var correlationId = Uuid.FromInt64(response.CorrelationId.Structured.MostSignificantBits,
								response.CorrelationId.Structured.LeastSignificantBits);

							if (_responses.TryRemove(correlationId, out var tcs)) {
								tcs.TrySetResult(response);
							}
						}
					} catch (Exception ex) {
						foreach (var kvp in _responses) {
							kvp.Value.TrySetException(ex);
						}
					}
				});

			public ValueTask DisposeAsync() =>
				_batchAppendLazy.IsValueCreated
					? new(_batchAppendLazy.Value.RequestStream.CompleteAsync())
					: new(Task.CompletedTask);

			public async ValueTask<BatchAppendResp> Call(params BatchAppendReq[] requests) {
				if (requests.Length == 0) {
					throw new ArgumentException($"Must have at least one {nameof(BatchAppendReq)}.",
						nameof(requests));
				}

				if (requests.Select(r => r.CorrelationId).Distinct().Count() > 1) {
					throw new ArgumentException($"All {nameof(BatchAppendReq)} must have same CorrelationId.",
						nameof(requests));
				}
				var tcs = new TaskCompletionSource<BatchAppendResp>();
				var correlationId = Uuid.FromInt64(requests[0].CorrelationId.Structured.MostSignificantBits,
					requests[0].CorrelationId.Structured.LeastSignificantBits);

				if (!_responses.TryAdd(correlationId, tcs)) {
					throw new ArgumentException("CorrelationId is already reserved.", nameof(correlationId));
				}

				foreach (var request in requests) {
					await BatchAppend.RequestStream.WriteAsync(request);
				}

				return await tcs.Task;
			}
		}
	}
}
