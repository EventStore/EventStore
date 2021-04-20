namespace EventStore.Core.LogAbstraction {
	public interface IStreamNameIndex<TStreamId> {
		bool GetOrAddId(string streamName, out TStreamId streamId);
	}
}
