// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace EventStore.Transport.Tcp;

public interface ITcpConnection {
	event Action<ITcpConnection, SocketError> ConnectionClosed;

	Guid ConnectionId { get; }
	string ClientConnectionName { get; }
	IPEndPoint RemoteEndPoint { get; }
	IPEndPoint LocalEndPoint { get; }
	int SendQueueSize { get; }
	int PendingSendBytes { get; }
	long TotalBytesSent { get; }
	long TotalBytesReceived { get; }
	bool IsClosed { get; }

	void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback);
	void EnqueueSend(IEnumerable<ArraySegment<byte>> data);
	void Close(string reason);
	void SetClientConnectionName(string clientConnectionName);
}
