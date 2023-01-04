namespace EventStore.Core.Data.Redaction {
	public readonly struct EventPosition {
		public long LogPosition { get; }
		public string ChunkFile { get; }
		public byte ChunkVersion { get; }
		public uint ChunkPosition { get; }
		public bool ChunkComplete { get; }

		public EventPosition(long logPosition, string chunkFile, byte chunkVersion, uint chunkPosition, bool chunkComplete) {
			LogPosition = logPosition;
			ChunkFile = chunkFile;
			ChunkVersion = chunkVersion;
			ChunkPosition = chunkPosition;
			ChunkComplete = chunkComplete;
		}
	}
}
