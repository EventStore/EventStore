using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClusterNode;
using EventStore.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.TestHost;
using NLog.Fluent;
using Xunit;

namespace EventStore.Grpc.Tests {
	public abstract class EventStoreGrpcFixture : IAsyncLifetime {
		public const string TestEventType = "-";
		private readonly ClusterVNode _node;
		public readonly EventStoreGrpcClient Client;
		private static readonly int ExternalPort = PortHelper.GetAvailablePort(IPAddress.Loopback);
		private readonly Func<CancellationToken, Task> _startServer;
		private readonly Action _disposeServer;

		public ClusterVNode Node => _node;

		static EventStoreGrpcFixture() {
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				return;
			}

			AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true); //TODO JPB Remove this sadness when dotnet core supports kestrel + http2 on macOS
		}

		protected EventStoreGrpcFixture(
			Action<VNodeBuilder> configureVNode = default,
			Action<IWebHostBuilder> configureWebHost = default,
			bool external = false) {
			var webHostBuilder = new WebHostBuilder();

			configureWebHost?.Invoke(webHostBuilder);

			var vNodeBuilder = new TestVNodeBuilder()
				.RunInMemory();
			configureVNode?.Invoke(vNodeBuilder);

			_node = vNodeBuilder.Build();

			if (external) {
				var host = webHostBuilder
					.UseKestrel(serverOptions => {
						serverOptions.Listen(IPAddress.Loopback, ExternalPort, listenOptions => {
							if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
								listenOptions.Protocols = HttpProtocols.Http2;
							} else {
								listenOptions.UseHttps();
							}
						});
					})
					.UseStartup(new ClusterVNodeStartup(_node))
					.Build();

				Client = new EventStoreGrpcClient(new UriBuilder {
					Port = ExternalPort,
					Scheme = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Uri.UriSchemeHttp : Uri.UriSchemeHttps
				}.Uri, () => new HttpClient(new SocketsHttpHandler {
					SslOptions = new SslClientAuthenticationOptions {
						RemoteCertificateValidationCallback = delegate { return true; }
					}
				}) {
					DefaultRequestVersion = new Version(2, 0)
				});
				_startServer = host.StartAsync;
				_disposeServer = host.Dispose;
			} else {
				var testServer = new TestServer(
					webHostBuilder.UseStartup(new ClusterVNodeStartup(_node)));

				Client = new EventStoreGrpcClient(new UriBuilder().Uri, () => {
					var client = testServer.CreateClient();
					client.DefaultRequestVersion = new Version(2, 0);
					return client;
				});
				_startServer = _ => Task.CompletedTask;
				_disposeServer = testServer.Dispose;
			}
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
			await _startServer(CancellationToken.None);
			await Given().WithTimeout(TimeSpan.FromMinutes(5));
			await When().WithTimeout(TimeSpan.FromMinutes(5));
		}

		public virtual async Task DisposeAsync() {
			await _node.Stop();
			_disposeServer();
			Client?.Dispose();
		}

		public string GetStreamName([CallerMemberName] string testMethod = default) {
			var type = GetType();

			return $"{type.DeclaringType.Name}_{testMethod ?? "unknown"}";
		}

		private static class PortHelper {
			private const int PortStart = 49152;
			private const int PortCount = ushort.MaxValue - PortStart;

			public static int GetAvailablePort(IPAddress ip) {
				var properties = IPGlobalProperties.GetIPGlobalProperties();

				var ipEndPoints = properties.GetActiveTcpConnections().Select(x => x.LocalEndPoint)
					.Concat(properties.GetActiveTcpListeners())
					.Concat(properties.GetActiveUdpListeners())
					.Where(x => x.AddressFamily == AddressFamily.InterNetwork &&
					            x.Address.Equals(ip) &&
					            x.Port >= PortStart &&
					            x.Port < PortStart + PortCount)
					.OrderBy(x => x.Port)
					.ToArray();

				var useablePorts = new HashSet<int>(Enumerable.Range(PortStart, PortCount));

				useablePorts.ExceptWith(ipEndPoints.Select(x => x.Port));
				return useablePorts.FirstOrDefault();
			}
		}
	}
}
