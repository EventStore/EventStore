namespace EventStore.Core.Data.Redaction {
	public readonly struct EventPosition {
		public long LogPosition { get; }
		public string ChunkFile { get; }
		public byte ChunkVersion { get; }
		public uint ChunkPosition { get; }

		public EventPosition(long logPosition, string chunkFile, byte chunkVersion, uint chunkPosition) {
			LogPosition = logPosition;
			ChunkFile = chunkFile;
			ChunkVersion = chunkVersion;
			ChunkPosition = chunkPosition;
		}
	}
}
