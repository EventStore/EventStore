using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EventStore.ClusterNode;
using EventStore.Core;
using EventStore.Core.TransactionLog.Chunks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Xunit;

namespace EventStore.Grpc.Tests {
	public class StandaloneKestrelServerFixture : IAsyncLifetime {
		private static readonly ClusterVNode Node;
		private static readonly TFChunkDb Db;
		private static readonly IWebHost Host;
		public static EventStoreGrpcClient Client { get; }

		static StandaloneKestrelServerFixture() {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
				AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport",
					true); //TODO JPB Remove this sadness when dotnet core supports kestrel + http2 on macOS
			}

			using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
			var port = ((IPEndPoint)socket.LocalEndPoint).Port;

			var vNodeBuilder = new TestVNodeBuilder();
			vNodeBuilder.RunInMemory();
			Node = vNodeBuilder.Build();
			Db = vNodeBuilder.GetDb();

			Host = new WebHostBuilder()
				.UseKestrel(serverOptions => {
					serverOptions.Listen(IPAddress.Loopback, port, listenOptions => {
						if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
							listenOptions.Protocols = HttpProtocols.Http2;
						} else {
							listenOptions.UseHttps();
						}
					});
				})
				.UseStartup(new ClusterVNodeStartup(Node))
				.Build();

			Client = new EventStoreGrpcClient(new UriBuilder {
				Port = port,
				Scheme = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? Uri.UriSchemeHttp : Uri.UriSchemeHttps
			}.Uri, () => new HttpClient(new SocketsHttpHandler {
				SslOptions = new SslClientAuthenticationOptions {
					RemoteCertificateValidationCallback = delegate { return true; }
				}
			}) {
				DefaultRequestVersion = new Version(2, 0)
			});
		}

		async Task IAsyncLifetime.InitializeAsync() {
			await Node.StartAndWaitUntilReady();
			await Host.StartAsync();
		}

		async Task IAsyncLifetime.DisposeAsync() {
			await Node.Stop();
			Db.Dispose();
			await Host.StopAsync();
			Host.Dispose();
			Client.Dispose();
		}
	}
}
