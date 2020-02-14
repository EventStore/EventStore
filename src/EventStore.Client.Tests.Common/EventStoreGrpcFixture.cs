using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core;
using EventStore.Core.TransactionLog.Chunks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventStore.Client {
	public abstract class EventStoreGrpcFixture : IAsyncLifetime {
		public const string TestEventType = "-";
		private readonly TFChunkDb _db;

		protected TestServer TestServer { get; }
		public ClusterVNode Node { get; }
		public EventStoreClient Client { get; }

		protected EventStoreGrpcFixture(
			Action<VNodeBuilder> configureVNode = default,
			Action<IWebHostBuilder> configureWebHost = default,
			EventStoreClientSettings clientSettings = default) {
			var webHostBuilder = new WebHostBuilder();
			configureWebHost?.Invoke(webHostBuilder);

			var vNodeBuilder = new TestVNodeBuilder();
			vNodeBuilder.RunInMemory().WithTfChunkSize(1024 * 1024);
			configureVNode?.Invoke(vNodeBuilder);

			Node = vNodeBuilder.Build();
			_db = vNodeBuilder.GetDb();

			TestServer = new TestServer(
				webHostBuilder
					.UseStartup(new TestClusterVNodeStartup(Node)));

			var settings = clientSettings ?? new EventStoreClientSettings() {
				CreateHttpMessageHandler = () => new ResponseVersionHandler {
					InnerHandler = TestServer.CreateHandler()
				}
			};

			Client = new EventStoreClient(settings);
		}


		protected abstract Task Given();
		protected abstract Task When();

		public IEnumerable<EventData> CreateTestEvents(int count = 1, string type = default)
			=> Enumerable.Range(0, count).Select(index => CreateTestEvent(index, type ?? TestEventType));

		protected static EventData CreateTestEvent(int index) => CreateTestEvent(index, TestEventType);

		protected static EventData CreateTestEvent(int index, string type)
			=> new EventData(Uuid.NewUuid(), type, Encoding.UTF8.GetBytes($@"{{""x"":{index}}}"));

		public virtual async Task InitializeAsync() {
			await Node.StartAsync(true);
			await Given().WithTimeout(TimeSpan.FromMinutes(5));
			await When().WithTimeout(TimeSpan.FromMinutes(5));
		}

		public virtual async Task DisposeAsync() {
			await Node.StopAsync();
			_db.Dispose();
			TestServer.Dispose();
			Client?.Dispose();
		}

		public string GetStreamName([CallerMemberName] string testMethod = default) {
			var type = GetType();

			return $"{type.DeclaringType.Name}_{testMethod ?? "unknown"}";
		}


		protected class ResponseVersionHandler : DelegatingHandler {
			protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
				CancellationToken cancellationToken) {
				var response = await base.SendAsync(request, cancellationToken);
				response.Version = request.Version;

				return response;
			}
		}
		

		protected class DelayedHandler : HttpClientHandler {
			private readonly int _delay;

			public DelayedHandler(int delay) {
				_delay = delay;
			}
			protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
				CancellationToken cancellationToken) {
				await Task.Delay(_delay, cancellationToken);
				return await base.SendAsync(request, cancellationToken);
			}
		}

		public class TestClusterVNodeStartup : IStartup {
			private readonly ClusterVNode _node;

			public TestClusterVNodeStartup(ClusterVNode node) {
				if (node == null) throw new ArgumentNullException(nameof(node));
				_node = node;
			}

			public IServiceProvider ConfigureServices(IServiceCollection services) =>
				_node.Startup.ConfigureServices(services);

			public void Configure(IApplicationBuilder app) => _node.Startup.Configure(app.Use(CompleteResponse));

			private static RequestDelegate CompleteResponse(RequestDelegate next) => context =>
				next(context).ContinueWith(_ => context.Response.Body.FlushAsync());
		}
	}
}
