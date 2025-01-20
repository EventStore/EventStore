// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Net.Sockets;
using EventStore.Core.Services.Transport.Tcp;

namespace KurrentDB.TestClient.Commands;

internal class ScavengeProcessor : ICmdProcessor {
	public string Usage {
		get { return Keyword; }
	}

	public string Keyword {
		get { return "SCAVENGE"; }
	}

	public bool Execute(CommandProcessorContext context, string[] args) {
		var package = new TcpPackage(TcpCommand.ScavengeDatabase, Guid.NewGuid(), null);
		context.Log.Information("Sending SCAVENGE request...");

		var connection = context._tcpTestClient.CreateTcpConnection(
			context,
			(conn, pkg) => { },
			null,
			(typedConnection, error) => {
				if (error == SocketError.Success)
					context.Success();
				else
					context.Fail();
			});
		connection.EnqueueSend(package.AsByteArray());
		connection.Close("OK");
		return true;
	}
}
