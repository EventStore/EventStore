namespace EventStore.Transport.Tcp {
	public static class TcpConfiguration {
		public const int SocketCloseTimeoutMs = 500;

		public const int AcceptBacklogCount = 1000;
		public const int ConcurrentAccepts = 1;
		public const int AcceptPoolSize = ConcurrentAccepts * 2;

		public const int ConnectPoolSize = 32;
		public const int SendReceivePoolSize = 512;

		public const int BufferChunksCount = 512;
		public const int SocketBufferSize = 8 * 1024;
	}
}
