// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net;
using System.Security.Cryptography.X509Certificates;
using EventStore.Core;
using EventStore.Core.Certificates;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Services.Transport.Tcp;
using EventStore.Plugins.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EventStore.TcpUnitTestPlugin;

public class PublicTcpApiTestService : IHostedService {
	private static readonly ILogger Log = Serilog.Log.ForContext<PublicTcpApiTestService>();

	public PublicTcpApiTestService(IServiceProvider provider) {
		var components = provider.GetRequiredService<StandardComponents>();
		var conf = provider.GetRequiredService<TcpTestOptions>();
		var authGateway = provider.GetRequiredService<AuthorizationGateway>();
		var authProvider = provider.GetRequiredService<IAuthenticationProvider>();

		var endpoint = new IPEndPoint(IPAddress.Loopback, conf.NodeTcpPort);
		TcpService tcpService;
		if (conf.Insecure) {
			tcpService = new TcpService(components.MainQueue, endpoint, components.NetworkSendService,
				TcpServiceType.External, TcpSecurityType.Normal,
				new ClientTcpDispatcher(conf.WriteTimeoutMs),
				TimeSpan.FromMilliseconds(conf.NodeHeartbeatInterval),
				TimeSpan.FromMilliseconds(conf.NodeHeartbeatTimeout),
				authProvider,
				authGateway,
				null,
				null,
				null,
				conf.ConnectionPendingSendBytesThreshold,
				conf.ConnectionQueueSizeThreshold);
		} else {
			var certificateProvider = provider.GetService<CertificateProvider>();
			tcpService = new TcpService(components.MainQueue, endpoint, components.NetworkSendService,
				TcpServiceType.External, TcpSecurityType.Secure,
				new ClientTcpDispatcher(conf.WriteTimeoutMs),
				TimeSpan.FromMilliseconds(conf.NodeHeartbeatInterval),
				TimeSpan.FromMilliseconds(conf.NodeHeartbeatTimeout),
				authProvider,
				authGateway,
				() => certificateProvider?.Certificate,
				() => {
					var intermediates = certificateProvider?.IntermediateCerts;
					return intermediates == null
						? null
						: new X509Certificate2Collection(intermediates);
				},
				delegate { return (true, null); },
				conf.ConnectionPendingSendBytesThreshold,
				conf.ConnectionQueueSizeThreshold);
		}

		components.MainBus.Subscribe<SystemMessage.SystemInit>(tcpService);
		components.MainBus.Subscribe<SystemMessage.SystemStart>(tcpService);
		components.MainBus.Subscribe<SystemMessage.BecomeShuttingDown>(tcpService);

		Task.Run(async () => {
			await Task.Delay(TimeSpan.FromHours(1));
			Log.Warning("Shutting down TCP unit tests");
			tcpService.Handle(new SystemMessage.BecomeShuttingDown(Guid.NewGuid(), true, true));
		});
	}

	public Task StartAsync(CancellationToken cancellationToken) {
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) {
		return Task.CompletedTask;
	}
}
