using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClusterNode;
using EventStore.Core;
using EventStore.Core.TransactionLog.Chunks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Xunit;

namespace EventStore.Grpc {
	public class StandaloneKestrelServerFixture : IAsyncLifetime {
		private readonly ClusterVNode _node;
		private readonly TFChunkDb _db;
		private readonly IWebHost _host;
		public EventStoreGrpcClient Client { get; }

		public StandaloneKestrelServerFixture() {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport",
					true); //TODO JPB Remove this sadness when dotnet core supports kestrel + http2 on macOS
			}

			using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
			var port = ((IPEndPoint)socket.LocalEndPoint).Port;

			var vNodeBuilder = new TestVNodeBuilder();
			vNodeBuilder.RunInMemory();
			_node = vNodeBuilder.Build();
			_db = vNodeBuilder.GetDb();

			_host = new WebHostBuilder()
				.UseKestrel(serverOptions => {
					serverOptions.Listen(IPAddress.Loopback, port, listenOptions => {
						if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
							listenOptions.Protocols = HttpProtocols.Http2;
						} else {
							listenOptions.UseHttps();
						}
					});
				})
				.UseStartup(_node.Startup)
				.Build();

			Client = new EventStoreGrpcClient(new UriBuilder {
				Port = port,
				Scheme = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Uri.UriSchemeHttp : Uri.UriSchemeHttps
			}.Uri, () => new HttpClient(new SocketsHttpHandler {
				SslOptions = new SslClientAuthenticationOptions {
					RemoteCertificateValidationCallback = delegate { return true; }
				}
			}) {
				DefaultRequestVersion = new Version(2, 0),
				Timeout = Timeout.InfiniteTimeSpan
			});
		}

		public virtual async Task InitializeAsync() {
			await _node.StartAsync(true);
			await _host.StartAsync();
		}

		public virtual async Task DisposeAsync() {
			await _node.StopAsync();
			_db.Dispose();
			await _host.StopAsync();
			_host.Dispose();
			Client.Dispose();
		}
	}
}
