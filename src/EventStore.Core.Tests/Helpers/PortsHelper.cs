// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net;
using System.Net.Sockets;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Tests.Helpers;

public static class PortsHelper {
	private static readonly ILogger Log =
		Serilog.Log.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "PortsHelper");
	public static int GetAvailablePort(IPAddress ip) {
		TcpListener l = new TcpListener(ip, 0);
		l.Start();
		int port = ((IPEndPoint)l.LocalEndpoint).Port;
		l.Stop();
		Log.Information($"Available port found: {port}");
		return port;
	}

	public static IPEndPoint GetLoopback() {
		var ip = IPAddress.Loopback;
		int port = PortsHelper.GetAvailablePort(ip);
		return new IPEndPoint(ip, port);
	}
}
