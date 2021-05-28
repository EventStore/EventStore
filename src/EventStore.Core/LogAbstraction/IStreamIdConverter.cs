namespace EventStore.Core.LogAbstraction {
	// Converts from long to streamId
	// trivial conversion for v3 and non-existent for v2
	public interface IStreamIdConverter<TStreamId> {
		TStreamId ToStreamId(long x);
	}
}
