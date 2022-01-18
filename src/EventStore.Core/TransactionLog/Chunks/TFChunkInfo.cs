namespace EventStore.Core.TransactionLog.Chunks {
	public record TFChunkInfo(string fileName);
	public record LatestVersion(string fileName, int start, int end) : TFChunkInfo(fileName);
	public record OldVersion(string fileName, int start) : TFChunkInfo(fileName);
	public record MissingVersion(string fileName, int start) : TFChunkInfo(fileName);
}
