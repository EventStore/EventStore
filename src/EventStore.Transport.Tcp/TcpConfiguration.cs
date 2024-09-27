// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Transport.Tcp {
	public static class TcpConfiguration {
		public const int SocketCloseTimeoutSecs = 1;

		public const int AcceptBacklogCount = 128;
		public const int ConcurrentAccepts = 1;
		public const int AcceptPoolSize = ConcurrentAccepts * 2;

		public const int ConnectPoolSize = 32;
		public const int SendReceivePoolSize = 512;

		public const int BufferChunksCount = 512;
		public const int SocketBufferSize = 8 * 1024;
	}
}
