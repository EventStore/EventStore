// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net;

namespace KurrentDB.TestClient.Commands.RunTestScenarios;

internal class NodeConnectionInfo {
	public IPAddress IpAddress { get; private set; }
	public int TcpPort { get; private set; }
	public int HttpPort { get; private set; }

	public NodeConnectionInfo(IPAddress ipAddress, int tcpPort, int httPort) {
		IpAddress = ipAddress;
		TcpPort = tcpPort;
		HttpPort = httPort;
	}

	public override string ToString() {
		return string.Format("[{0}:{1}:{2}]", IpAddress, TcpPort, HttpPort);
	}
}
