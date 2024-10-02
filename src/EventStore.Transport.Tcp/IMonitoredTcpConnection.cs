// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Transport.Tcp {
	public interface IMonitoredTcpConnection {
		bool IsReadyForSend { get; }
		bool IsReadyForReceive { get; }
		bool IsInitialized { get; }
		bool IsFaulted { get; }
		bool IsClosed { get; }

		bool InSend { get; }
		bool InReceive { get; }

		DateTime? LastSendStarted { get; }
		DateTime? LastReceiveStarted { get; }

		int PendingSendBytes { get; }
		int InSendBytes { get; }
		int PendingReceivedBytes { get; }

		long TotalBytesSent { get; }
		long TotalBytesReceived { get; }
	}
}
