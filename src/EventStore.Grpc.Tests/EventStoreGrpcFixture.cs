using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClusterNode;
using EventStore.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace EventStore.Grpc.Tests {
	public abstract class EventStoreGrpcFixture : IAsyncLifetime {
		public const string TestEventType = "-";
		private readonly ClusterVNode _node;
		public readonly EventStoreGrpcClient Client;
		private readonly TestServer _testServer;

		public ClusterVNode Node => _node;

		static EventStoreGrpcFixture() {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				return;
			}

			AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport",
				true); //TODO JPB Remove this sadness when dotnet core supports kestrel + http2 on macOS
		}

		protected EventStoreGrpcFixture(
			Action<VNodeBuilder> configureVNode = default,
			Action<IWebHostBuilder> configureWebHost = default) {
			var webHostBuilder = new WebHostBuilder();

			configureWebHost?.Invoke(webHostBuilder);

			var vNodeBuilder = new TestVNodeBuilder();
			vNodeBuilder.RunInMemory().WithTfChunkSize(1024 * 1024);
			configureVNode?.Invoke(vNodeBuilder);

			_node = vNodeBuilder
				.Build();

			_testServer = new TestServer(
				webHostBuilder.UseStartup(new ClusterVNodeStartup(_node)));

			Client = new EventStoreGrpcClient(new UriBuilder().Uri, _testServer.CreateClient);
		}

		protected abstract Task Given();
		protected abstract Task When();

		public IEnumerable<EventData> CreateTestEvents(int count = 1, string type = default)
			=> Enumerable.Range(0, count).Select(index => CreateTestEvent(index, type ?? TestEventType));

		protected static EventData CreateTestEvent(int index) => CreateTestEvent(index, TestEventType);

		protected static EventData CreateTestEvent(int index, string type)
			=> new EventData(Uuid.NewUuid(), type, Encoding.UTF8.GetBytes($@"{{""x"":{index}}}"));

		public virtual async Task InitializeAsync() {
			await _node.StartAndWaitUntilReady();
			await Given();
			await When();
		}

		public virtual async Task DisposeAsync() {
			await _node.Stop();
			_testServer?.Dispose();
		}

		public string GetStreamName([CallerMemberName] string testMethod = default) {
			var type = GetType();

			return $"{type.DeclaringType.Name}_{testMethod ?? "unknown"}";
		}
	}
}
