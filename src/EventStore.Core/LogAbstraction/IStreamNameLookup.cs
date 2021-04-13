namespace EventStore.Core.LogAbstraction {
	public interface IStreamNameLookup<TStreamId> {
		string LookupName(TStreamId streamId);
	}
}
