namespace EventStore.Core.Data.Redaction {
	public readonly struct EventPosition {
		public long LogPosition { get; }
		public string ChunkFile { get; }

		public EventPosition(long logPosition, string chunkFile) {
			LogPosition = logPosition;
			ChunkFile = chunkFile;
		}
	}
}
