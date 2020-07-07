using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using EventStore.ClusterNode;
using EventStore.Core;
using EventStore.Core.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public partial class EventStoreClientAPIClusterFixture : IAsyncLifetime, IAsyncDisposable {
		private const int ClusterSize = 3;
		private readonly ClusterVNode[] _nodes = new ClusterVNode[ClusterSize];
		private readonly IWebHost[] _hosts = new IWebHost[ClusterSize];
		public EventStoreClientAPIClusterFixture() {
			var serverCertificate = GetServerCertificate();
			var rootCertificates = new X509Certificate2Collection(GetRootCertificate());
			for (var i = 0; i < ClusterSize; i++) {
				var vNodeBuilder = ClusterVNodeBuilder
					.AsClusterMember(ClusterSize)
					.DisableDnsDiscovery()
					.WithGossipSeeds(GetGossipSeedEndPointsExceptFor(i, false))
					.WithHttpOn(new IPEndPoint(IPAddress.Loopback, HttpPort[i]))
					.WithInternalTcpOn(new IPEndPoint(IPAddress.Loopback, InternalTcpPort[i]))
					.WithInternalSecureTcpOn(new IPEndPoint(IPAddress.Loopback, InternalSecureTcpPort[i]))
					.WithExternalTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalTcpPort[i]))
					.WithExternalSecureTcpOn(new IPEndPoint(IPAddress.Loopback, ExternalSecureTcpPort[i]))
					.WithServerCertificate(serverCertificate)
					.WithTrustedRootCertificates(rootCertificates)
					.WithCertificateReservedNodeCommonName(Opts.CertificateReservedNodeCommonNameDefault)
					.RunInMemory()
					.EnableExternalTCP();

				_nodes[i] = vNodeBuilder.Build();

				var httpEndPoint = new IPEndPoint(IPAddress.Loopback, HttpPort[i]);

				_hosts[i] = new WebHostBuilder()
					.UseKestrel(o => {
						o.Listen(httpEndPoint, options => {
							if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
								options.Protocols = HttpProtocols.Http2;
							} else {
								options.UseHttps(new HttpsConnectionAdapterOptions {
									ServerCertificate = serverCertificate,
									ClientCertificateMode = ClientCertificateMode.AllowCertificate,
									ClientCertificateValidation = (certificate, chain, sslPolicyErrors) => {
										var (isValid, error) =
											ClusterVNode.ValidateClientCertificateWithTrustedRootCerts(certificate, chain, sslPolicyErrors, () => rootCertificates);
										if (!isValid && error != null) {
											Log.Error("Client certificate validation error: {e}", error);
										}
										return isValid;
									}
								});
							}
						});
					})
					.ConfigureServices(services => services.AddSingleton(_nodes[i].Startup))
					.Build();
			}
		}

		private X509Certificate2 GetServerCertificate() {
			using var stream = typeof(EventStoreClientAPIClusterFixture)
            	.Assembly
            	.GetManifestResourceStream(typeof(EventStoreClientAPIClusterFixture), "server.pfx");
            using var mem = new MemoryStream();
            stream.CopyTo(mem);

            return new X509Certificate2(mem.ToArray(), "changeit");
		}

		private X509Certificate2 GetRootCertificate() {
			using var stream = typeof(EventStoreClientAPIClusterFixture)
				.Assembly
				.GetManifestResourceStream(typeof(EventStoreClientAPIClusterFixture), "ca.pem");
			using var mem = new MemoryStream();
			stream.CopyTo(mem);

			return new X509Certificate2(mem.ToArray(), "changeit");
		}

		private EndPoint[] GetGossipSeedEndPointsExceptFor(int nodeIndex, bool dnsEndpoint) {
			List<EndPoint> endPoints = new List<EndPoint>();
			for (var i = 0; i < ClusterSize; i++) {
				if (i != nodeIndex) {
					if (dnsEndpoint) {
						endPoints.Add(new DnsEndPoint("localhost", HttpPort[i]));
					} else {
						endPoints.Add(new IPEndPoint(IPAddress.Loopback, HttpPort[i]));
					}
				}
			}
			return endPoints.ToArray();
		}

		public async Task InitializeAsync() {
			await Task.WhenAll(_hosts.Select(host => host.StartAsync())).WithTimeout(TimeSpan.FromSeconds(10));
			await Task.WhenAll(_nodes.Select(node => node.StartAsync(false))).WithTimeout(TimeSpan.FromSeconds(15));
			await Task.Delay(10000); //TODO(shaan1337): see how to improve this
		}

		public async Task DisposeAsync() {
			await Task.WhenAll(_nodes.Select(node => node.StopAsync())).WithTimeout(TimeSpan.FromSeconds(15));
			await Task.WhenAll(_hosts.Select(host => host.StopAsync())).WithTimeout(TimeSpan.FromSeconds(10));
		}

		ValueTask IAsyncDisposable.DisposeAsync() => new ValueTask(DisposeAsync());
	}
}
