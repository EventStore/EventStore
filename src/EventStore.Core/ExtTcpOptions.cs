using System;

namespace EventStore.Core;

public class ExtTcpOptions {
	public int Port { get; private set; }
	public int? AdvertisedPort { get; private set; }

	public int HeartbeatTimeout { get; private set; } = 1_000;
	public int HeartbeatInterval { get; private set; } = 2_000;

	public static bool TryParse(out ExtTcpOptions options) {
		options = null;

		if (!bool.TryParse(Environment.GetEnvironmentVariable(ClusterVNode.TcpApiEnvVar), out var tcpApi) || !tcpApi)
			return false;

		options = new ExtTcpOptions();

		if (!int.TryParse(Environment.GetEnvironmentVariable(ClusterVNode.TcpApiPortEnvVar), out var tcpApiPort) && tcpApiPort <= 0)
			return false;

		options.Port = tcpApiPort;

		if (int.TryParse(Environment.GetEnvironmentVariable(ClusterVNode.TcpApiAdvertisedPortEnvVar),
			    out var tcpApiAdvertisedPort) && tcpApiAdvertisedPort > 0)
			options.AdvertisedPort = tcpApiAdvertisedPort;

		if (int.TryParse(Environment.GetEnvironmentVariable(ClusterVNode.TcpApiHeartbeatTimeoutEnvVar),
			    out var tcpHeartbeatTimeout) && tcpHeartbeatTimeout > 0)
			options.HeartbeatTimeout = tcpHeartbeatTimeout;

		if (int.TryParse(Environment.GetEnvironmentVariable(ClusterVNode.TcpApiHeartbeatIntervalEnvVar),
			    out var tcpHeartbeatInterval) && tcpHeartbeatInterval > 0)
			options.HeartbeatInterval = tcpHeartbeatInterval;

		return true;
	}
}
