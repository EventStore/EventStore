namespace EventStore.Core.LogAbstraction {
	public interface IStreamIdLookup<TStreamId> {
		TStreamId LookupId(string streamName);
	}
}
