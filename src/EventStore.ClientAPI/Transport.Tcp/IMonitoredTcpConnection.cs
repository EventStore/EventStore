using System;

namespace EventStore.ClientAPI.Transport.Tcp {
	internal interface IMonitoredTcpConnection {
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
