namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IChunkReaderForIndexExecutor<TStreamId> {
		bool TryGetStreamId(long position, out TStreamId streamId);
	}
}
