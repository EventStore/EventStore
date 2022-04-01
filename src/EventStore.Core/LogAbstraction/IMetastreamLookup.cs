namespace EventStore.Core.LogAbstraction {
	public interface IMetastreamLookup<TStreamId> {
		bool IsMetaStream(TStreamId streamId);
		TStreamId MetaStreamOf(TStreamId streamId);
		TStreamId OriginalStreamOf(TStreamId streamId);
	}
}
